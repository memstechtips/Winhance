using System;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Events.UI
{
    /// <summary>
    /// Event for log messages
    /// </summary>
    public class LogEvent : IDomainEvent
    {
        /// <summary>
        /// Gets the event ID
        /// </summary>
        public Guid EventId { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the timestamp when the event was created
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the log message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the log level
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the exception associated with the log message
        /// </summary>
        public Exception? Exception { get; set; }
    }
}
