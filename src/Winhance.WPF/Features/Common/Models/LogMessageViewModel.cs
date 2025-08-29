using System;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// View model for log messages displayed in the UI.
    /// </summary>
    public class LogMessageViewModel
    {
        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the log level.
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets the formatted message with timestamp.
        /// </summary>
        public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] {Message}";
    }
}