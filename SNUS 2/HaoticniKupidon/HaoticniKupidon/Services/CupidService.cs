using HaoticniKupidon.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Threading;

namespace HaoticniKupidon.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class CupidService : IPersonService, IDisposable
    {
        private const int LetterIntervalMilliseconds = 60000;
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, RegisteredPerson> people =
            new Dictionary<string, RegisteredPerson>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer timer;

        public CupidService()
        {
            // Seed some premade people so the service can match and send letters
            // even when only a single real client is connected.
            SeedFakePeople();

            // Trigger first delivery immediately for quicker feedback during testing,
            // then continue with the regular interval.
            timer = new Timer(SendLetters, null, 0, LetterIntervalMilliseconds);
        }

        public OperationResult InitSinglePerson(SinglePerson person)
        {
            if (person == null)
            {
                return OperationResult.Fail("Podaci osobe nisu poslati.");
            }

            if (string.IsNullOrWhiteSpace(person.Username) ||
                string.IsNullOrWhiteSpace(person.City) ||
                person.Age <= 0 ||
                string.IsNullOrWhiteSpace(person.PhoneNumber))
            {
                return OperationResult.Fail("Podaci osobe nisu validni.");
            }

            ICupidCallback callback = OperationContext.Current.GetCallbackChannel<ICupidCallback>();

            lock (syncRoot)
            {
                if (people.ContainsKey(person.Username))
                {
                    return OperationResult.Fail("Username je vec zauzet.");
                }

                people[person.Username] = new RegisteredPerson
                {
                    Person = person,
                    Callback = callback
                };
            }

            Console.WriteLine("Prijavljena osoba: {0} ({1}, {2})", person.Username, person.City, person.Age);
            return OperationResult.Ok("Uspesno ste prijavljeni kod Haoticnog kupidona.");
        }

        public void ConfirmLetterReceived(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            lock (syncRoot)
            {
                RegisteredPerson person;
                if (people.TryGetValue(username, out person))
                {
                    person.WaitingForConfirmation = false;
                }
            }

            Console.WriteLine("{0} je potvrdio/la prijem pisma.", username);
        }

        public OperationResult BlockUser(string username, string usernameToBlock)
        {
            if (string.IsNullOrWhiteSpace(usernameToBlock))
            {
                return OperationResult.Fail("Unesite username koji zelite da blokirate.");
            }

            if (string.Equals(username, usernameToBlock, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail("Ne mozete blokirati sami sebe.");
            }

            lock (syncRoot)
            {
                RegisteredPerson person;
                if (!people.TryGetValue(username, out person))
                {
                    return OperationResult.Fail("Niste prijavljeni na servis.");
                }

                person.BlockedUsers.Add(usernameToBlock);
            }

            return OperationResult.Ok("Blokiran korisnik: " + usernameToBlock);
        }

        public void Dispose()
        {
            timer.Dispose();
        }

        private void SendLetters(object state)
        {
            Console.WriteLine("[CupidService] SendLetters triggered.");
            List<Delivery> deliveries = new List<Delivery>();

            lock (syncRoot)
            {
                // Only consider receivers that have an active callback (connected clients).
                foreach (RegisteredPerson receiver in people.Values.ToList())
                {
                    if (receiver == null || receiver.Callback == null)
                    {
                        if (receiver != null)
                        {
                            Console.WriteLine("[CupidService] Skipping {0} (no callback)", receiver.Person.Username);
                        }
                        continue;
                    }

                    if (receiver.WaitingForConfirmation)
                    {
                        Console.WriteLine("[CupidService] Skipping {0} (waiting confirmation)", receiver.Person.Username);
                        continue;
                    }

                    RegisteredPerson sender = FindBestSender(receiver);
                    if (sender == null)
                    {
                        Console.WriteLine("[CupidService] No sender found for {0}", receiver.Person.Username);
                        continue;
                    }

                    LoveLetter letter = CreateLetter(sender.Person);
                    receiver.WaitingForConfirmation = true;
                    deliveries.Add(new Delivery(receiver, letter));
                }
            }

            foreach (Delivery delivery in deliveries)
            {
                try
                {
                    delivery.Receiver.Callback.ReceiveLoveLetter(delivery.Letter);
                    Console.WriteLine("Pismo poslato za {0} od {1}.",
                        delivery.Receiver.Person.Username,
                        delivery.Letter.FromUsername);
                }
                catch (CommunicationException)
                {
                    MarkAsAvailable(delivery.Receiver.Person.Username);
                }
                catch (TimeoutException)
                {
                    MarkAsAvailable(delivery.Receiver.Person.Username);
                }
            }
        }

        private void SeedFakePeople()
        {
            var sample = new[]
            {
                new SinglePerson { Username = "alice", City = "Zagreb", Age = 28, PhoneNumber = "0911111111" },
                new SinglePerson { Username = "bob", City = "Zagreb", Age = 30, PhoneNumber = "0912222222" },
                new SinglePerson { Username = "carol", City = "Split", Age = 27, PhoneNumber = "0913333333" },
                new SinglePerson { Username = "dave", City = "Rijeka", Age = 31, PhoneNumber = "0914444444" },
                new SinglePerson { Username = "eve", City = "Split", Age = 26, PhoneNumber = "0915555555" },
                new SinglePerson { Username = "sarah", City = "Novi Sad", Age = 25, PhoneNumber = "0916666666" },
            };

            lock (syncRoot)
            {
                foreach (var p in sample)
                {
                    if (!people.ContainsKey(p.Username))
                    {
                        // Callback=null means these are virtual users usable as senders
                        // but they won't receive letters themselves.
                        people[p.Username] = new RegisteredPerson { Person = p, Callback = null };
                    }
                }
            }
        }

        private RegisteredPerson FindBestSender(RegisteredPerson receiver)
        {
            var best = people.Values
                .Where(candidate => !string.Equals(
                    candidate.Person.Username,
                    receiver.Person.Username,
                    StringComparison.OrdinalIgnoreCase))
                .Where(candidate => !receiver.BlockedUsers.Contains(candidate.Person.Username))
                .Select(candidate => new
                {
                    Person = candidate,
                    Score = CalculateScore(receiver.Person, candidate.Person)
                })
                .OrderByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            return best == null ? null : best.Person;
        }

        private int CalculateScore(SinglePerson receiver, SinglePerson sender)
        {
            int score = 0;

            if (string.Equals(receiver.City, sender.City, StringComparison.OrdinalIgnoreCase))
            {
                score += 30;
            }

            if (Math.Abs(receiver.Age - sender.Age) <= 2)
            {
                score += 20;
            }

            score += GetSecureRandomNumber(0, 101);
            return score;
        }

        private LoveLetter CreateLetter(SinglePerson sender)
        {
            string[] messages =
            {
                "Radujem se nasem susretu!",
                "Zelim da se upoznamo.",
                "Nisam zainteresovan/a za upoznavanje."
            };

            string message = messages[GetSecureRandomNumber(0, messages.Length)];
            bool showPhone = !string.Equals(
                message,
                "Nisam zainteresovan/a za upoznavanje.",
                StringComparison.Ordinal);

            return new LoveLetter
            {
                FromUsername = sender.Username,
                FromCity = sender.City,
                FromAge = sender.Age,
                FromPhoneNumber = sender.PhoneNumber,
                Message = message,
                ShowPhoneNumber = showPhone
            };
        }

        private int GetSecureRandomNumber(int minValue, int maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException("maxValue");
            }

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                int positiveValue = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
                return minValue + positiveValue % (maxValue - minValue);
            }
        }

        private void MarkAsAvailable(string username)
        {
            lock (syncRoot)
            {
                RegisteredPerson person;
                if (people.TryGetValue(username, out person))
                {
                    person.WaitingForConfirmation = false;
                }
            }
        }
    }
}
