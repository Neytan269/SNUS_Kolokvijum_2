using HaoticniKupidon.Contracts;
using System;
using System.Collections.Generic;

namespace HaoticniKupidon.Services
{
    internal class RegisteredPerson
    {
        public RegisteredPerson()
        {
            BlockedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public SinglePerson Person { get; set; }
        public ICupidCallback Callback { get; set; }
        public bool WaitingForConfirmation { get; set; }
        public HashSet<string> BlockedUsers { get; private set; }
    }
}
