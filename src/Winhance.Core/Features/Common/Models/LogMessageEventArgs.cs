using System;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Event arguments for log message events.
    /// </summary>
    public class LogMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the message content.
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Gets the log level.
        /// </summary>
        public LogLevel Level { get; }
        
        /// <summary>
        /// Gets the timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets the exception associated with the log message, if any.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogMessageEventArgs"/> class.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="level">The log level.</param>
        public LogMessageEventArgs(string message, LogLevel level)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Level = level;
            Timestamp = DateTime.Now;
            Exception = null;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="LogMessageEventArgs"/> class.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="message">The message content.</param>
        /// <param name="exception">The exception associated with the message, if any.</param>
        public LogMessageEventArgs(LogLevel level, string message, Exception? exception)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Level = level;
            Timestamp = DateTime.Now;
            Exception = exception;
        }
    }
}