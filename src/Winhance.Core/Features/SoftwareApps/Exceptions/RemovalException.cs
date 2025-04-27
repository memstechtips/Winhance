using System;

namespace Winhance.Core.Features.SoftwareApps.Exceptions
{
    /// <summary>
    /// Exception thrown when there is an error with removal operations.
    /// </summary>
    public class RemovalException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemovalException"/> class.
        /// </summary>
        public RemovalException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RemovalException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public RemovalException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RemovalException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public RemovalException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RemovalException"/> class with a specified item name, error message,
        /// a flag indicating whether the error is critical, and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="itemName">The name of the item that failed to be removed.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="isCritical">A flag indicating whether the error is critical.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public RemovalException(string itemName, string errorMessage, bool isCritical, Exception innerException)
            : base($"Failed to remove {itemName}: {errorMessage}", innerException)
        {
            ItemName = itemName;
            IsCritical = isCritical;
        }

        /// <summary>
        /// Gets the name of the item that failed to be removed.
        /// </summary>
        public string? ItemName { get; }

        /// <summary>
        /// Gets a value indicating whether the error is critical.
        /// </summary>
        public bool IsCritical { get; }
    }
}