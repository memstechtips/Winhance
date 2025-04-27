using System;

namespace Winhance.Core.Models.Exceptions
{
    public class InstallationStatusException : Exception
    {
        public InstallationStatusException(string message) : base(message)
        {
        }

        public InstallationStatusException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
