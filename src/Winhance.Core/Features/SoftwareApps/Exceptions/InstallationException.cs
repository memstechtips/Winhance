using System;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Exceptions
{
    /// <summary>
    /// Exception thrown when there is an error with installation operations.
    /// </summary>
    public class InstallationException : Exception
    {
        /// <summary>
        /// Gets the type of installation error.
        /// </summary>
        public InstallationErrorType ErrorType { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class.
        /// </summary>
        public InstallationException() : base()
        {
            ErrorType = InstallationErrorType.UnknownError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public InstallationException(string message) : base(message)
        {
            ErrorType = InstallationErrorType.UnknownError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public InstallationException(string message, Exception innerException) : base(message, innerException)
        {
            ErrorType = InstallationErrorType.UnknownError;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class with a specified error message,
        /// error type, and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="errorType">The type of installation error.</param>
        public InstallationException(string message, Exception innerException, InstallationErrorType errorType)
            : base(message, innerException)
        {
            ErrorType = errorType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class with a specified item name, error message,
        /// a flag indicating whether the error is critical, and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="itemName">The name of the item that failed to be installed.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="isCritical">A flag indicating whether the error is critical.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public InstallationException(string itemName, string errorMessage, bool isCritical, Exception innerException)
            : base($"Failed to install {itemName}: {errorMessage}", innerException)
        {
            ItemName = itemName;
            IsCritical = isCritical;
            ErrorType = InstallationErrorType.UnknownError;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class with a specified item name, error message,
        /// a flag indicating whether the error is critical, the error type, and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="itemName">The name of the item that failed to be installed.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="isCritical">A flag indicating whether the error is critical.</param>
        /// <param name="errorType">The type of installation error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public InstallationException(string itemName, string errorMessage, bool isCritical, InstallationErrorType errorType, Exception innerException)
            : base($"Failed to install {itemName}: {errorMessage}", innerException)
        {
            ItemName = itemName;
            IsCritical = isCritical;
            ErrorType = errorType;
        }

        /// <summary>
        /// Gets the name of the item that failed to be installed.
        /// </summary>
        public string? ItemName { get; }

        /// <summary>
        /// Gets a value indicating whether the error is critical.
        /// </summary>
        public bool IsCritical { get; }
    }
}