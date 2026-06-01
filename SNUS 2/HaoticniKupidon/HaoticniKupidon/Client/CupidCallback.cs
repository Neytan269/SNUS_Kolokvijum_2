using HaoticniKupidon.Contracts;
using System;

namespace HaoticniKupidon.Client
{
    public class CupidCallback : ICupidCallback
    {
        private readonly object syncRoot = new object();
        private bool hasUnconfirmedLetter;

        public bool HasUnconfirmedLetter
        {
            get
            {
                lock (syncRoot)
                {
                    return hasUnconfirmedLetter;
                }
            }
        }

        public void ReceiveLoveLetter(LoveLetter letter)
        {
            lock (syncRoot)
            {
                hasUnconfirmedLetter = true;
            }

            Console.WriteLine();
            Console.WriteLine("=== Stiglo je ljubavno pismo ===");
            Console.WriteLine("Od: {0}", letter.FromUsername);
            Console.WriteLine("Grad: {0}", letter.FromCity);
            Console.WriteLine("Godine: {0}", letter.FromAge);

            if (letter.ShowPhoneNumber)
            {
                Console.WriteLine("Telefon: {0}", letter.FromPhoneNumber);
            }

            Console.WriteLine("Poruka: {0}", letter.Message);
            Console.WriteLine("Pritisnite ENTER da potvrdite prijem pisma.");
            Console.WriteLine();
        }

        public void MarkConfirmed()
        {
            lock (syncRoot)
            {
                hasUnconfirmedLetter = false;
            }
        }
    }
}
