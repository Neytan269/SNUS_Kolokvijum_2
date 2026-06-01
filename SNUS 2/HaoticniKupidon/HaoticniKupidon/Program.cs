using HaoticniKupidon.Client;
using HaoticniKupidon.Contracts;
using HaoticniKupidon.Services;
using System;
using System.Linq;
using System.ServiceModel;

namespace HaoticniKupidon
{
    internal class Program
    {
        private const string ServiceAddress = "net.tcp://localhost:9009/HaoticniKupidon";

        static void Main(string[] args)
        {
            Console.Title = "Haoticni kupidon";

            ServiceHost host = TryStartHost();
            if (host != null)
            {
                Console.WriteLine("Ova instanca je centralni Haoticni kupidon server.");
            }
            else
            {
                Console.WriteLine("Centralni kupidon vec postoji. Povezivanje kao osoba...");
            }

            CupidCallback callback = new CupidCallback();
            InstanceContext callbackContext = new InstanceContext(callback);
            NetTcpBinding binding = CreateBinding();
            DuplexChannelFactory<IPersonService> factory =
                new DuplexChannelFactory<IPersonService>(callbackContext, binding, new EndpointAddress(ServiceAddress));

            IPersonService service = factory.CreateChannel();

            try
            {
                SinglePerson person = ReadPerson();
                OperationResult result = service.InitSinglePerson(person);

                if (!result.Success)
                {
                    Console.WriteLine(result.Message);
                    CloseCommunication(factory, host);
                    return;
                }

                Console.WriteLine(result.Message);
                Console.WriteLine("Komande: ENTER potvrda pisma, /block username, /exit");
                RunCommandLoop(service, callback, person.Username);
            }
            catch (EndpointNotFoundException)
            {
                Console.WriteLine("Nije moguce povezivanje na kupidon servis.");
            }
            catch (CommunicationException ex)
            {
                Console.WriteLine("Greska u komunikaciji: " + ex.Message);
            }
            finally
            {
                CloseCommunication(factory, host);
            }
        }

        private static ServiceHost TryStartHost()
        {
            CupidService service = new CupidService();
            ServiceHost host = new ServiceHost(service, new Uri(ServiceAddress));

            try
            {
                host.AddServiceEndpoint(typeof(IPersonService), CreateBinding(), "");
                host.Open();
                return host;
            }
            catch (AddressAlreadyInUseException)
            {
                host.Abort();
                service.Dispose();
                return null;
            }
            catch (CommunicationException)
            {
                host.Abort();
                service.Dispose();
                return null;
            }
        }

        private static NetTcpBinding CreateBinding()
        {
            NetTcpBinding binding = new NetTcpBinding(SecurityMode.None);
            binding.ReceiveTimeout = TimeSpan.FromMinutes(20);
            binding.SendTimeout = TimeSpan.FromSeconds(10);
            binding.OpenTimeout = TimeSpan.FromSeconds(10);
            binding.CloseTimeout = TimeSpan.FromSeconds(10);
            return binding;
        }

        private static SinglePerson ReadPerson()
        {
            Console.WriteLine();
            Console.WriteLine("Unesite podatke za prijavu.");

            return new SinglePerson
            {
                Username = ReadRequiredText("Username: "),
                City = ReadRequiredText("Grad: "),
                Age = ReadPositiveInt("Godine: "),
                PhoneNumber = ReadPhoneNumber("Broj telefona: ")
            };
        }

        private static string ReadRequiredText(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string value = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }

                Console.WriteLine("Polje ne sme biti prazno.");
            }
        }

        private static int ReadPositiveInt(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string value = Console.ReadLine();
                int number;

                if (!int.TryParse(value, out number))
                {
                    Console.WriteLine("Unesite broj, ne tekst ili karaktere.");
                    continue;
                }

                if (number <= 0)
                {
                    Console.WriteLine("Broj mora biti pozitivan.");
                    continue;
                }

                return number;
            }
        }

        private static string ReadPhoneNumber(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string value = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(value))
                {
                    Console.WriteLine("Broj telefona ne sme biti prazan.");
                    continue;
                }

                value = value.Trim();

                if (value.StartsWith("-", StringComparison.Ordinal))
                {
                    Console.WriteLine("Broj telefona ne sme biti negativan.");
                    continue;
                }

                if (!value.All(char.IsDigit))
                {
                    Console.WriteLine("Broj telefona sme da sadrzi samo cifre.");
                    continue;
                }

                return value;
            }
        }

        private static void RunCommandLoop(IPersonService service, CupidCallback callback, string username)
        {
            while (true)
            {
                string command = Console.ReadLine();

                if (command == null)
                {
                    break;
                }

                command = command.Trim();

                if (command.Length == 0)
                {
                    if (callback.HasUnconfirmedLetter)
                    {
                        service.ConfirmLetterReceived(username);
                        callback.MarkConfirmed();
                        Console.WriteLine("Potvrdili ste prijem pisma.");
                    }
                    else
                    {
                        Console.WriteLine("Nema pisma koje ceka potvrdu.");
                    }

                    continue;
                }

                if (string.Equals(command, "/exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (command.StartsWith("/block", StringComparison.OrdinalIgnoreCase))
                {
                    HandleBlockCommand(service, username, command);
                    continue;
                }

                Console.WriteLine("Nepoznata komanda. Koristite ENTER, /block username ili /exit.");
            }
        }

        private static void HandleBlockCommand(IPersonService service, string username, string command)
        {
            string[] parts = command.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                Console.WriteLine("Upotreba: /block username");
                return;
            }

            OperationResult result = service.BlockUser(username, parts[1].Trim());
            Console.WriteLine(result.Message);
        }

        private static void CloseCommunication(DuplexChannelFactory<IPersonService> factory, ServiceHost host)
        {
            try
            {
                if (factory != null)
                {
                    factory.Close();
                }
            }
            catch (CommunicationException)
            {
                if (factory != null)
                {
                    factory.Abort();
                }
            }

            if (host != null)
            {
                try
                {
                    host.Close();
                }
                catch (CommunicationException)
                {
                    host.Abort();
                }
            }
        }
    }
}
