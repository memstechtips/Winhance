using System;

namespace Winhance.Core.Features.SoftwareApps.Exceptions
{
    /// <summary>
    /// Exception thrown when there is an error with installation status operations.
    /// </summary>
    public class InstallationStatusException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationStatusException"/> class.
        /// </summary>
        public InstallationStatusException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationStatusException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public InstallationStatusException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationStatusException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public InstallationStatusException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}