using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents detailed progress information for a task.
    /// </summary>
    public class TaskProgressDetail
    {
        /// <summary>
        /// Gets or sets the progress value (0-100), or null if not applicable.
        /// </summary>
        public double? Progress { get; set; }

        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        public string StatusText { get; set; }

        /// <summary>
        /// Gets or sets a detailed message about the current operation.
        /// </summary>
        public string DetailedMessage { get; set; }

        /// <summary>
        /// Gets or sets the log level for the detailed message.
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// Gets or sets whether the progress is indeterminate.
        /// </summary>
        public bool IsIndeterminate { get; set; }

        /// <summary>
        /// Gets or sets additional information about the progress as key-value pairs.
        /// This can be used to provide more detailed information for logging or debugging.
        /// </summary>
        public Dictionary<string, string> AdditionalInfo { get; set; } = new Dictionary<string, string>();
    }
}