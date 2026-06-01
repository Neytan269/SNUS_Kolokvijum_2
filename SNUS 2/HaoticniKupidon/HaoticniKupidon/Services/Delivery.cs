namespace HaoticniKupidon.Services
{
    internal class Delivery
    {
        public Delivery(RegisteredPerson receiver, LoveLetter letter)
        {
            Receiver = receiver;
            Letter = letter;
        }

        public RegisteredPerson Receiver { get; private set; }
        public LoveLetter Letter { get; private set; }
    }
}
