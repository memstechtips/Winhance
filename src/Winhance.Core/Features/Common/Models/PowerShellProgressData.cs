using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents progress data from PowerShell operations.
    /// </summary>
    public class PowerShellProgressData
    {
        /// <summary>
        /// Gets or sets the percent complete value (0-100).
        /// </summary>
        public int? PercentComplete { get; set; }

        /// <summary>
        /// Gets or sets the activity description.
        /// </summary>
        public string Activity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status description.
        /// </summary>
        public string StatusDescription { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current operation description.
        /// </summary>
        public string CurrentOperation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the PowerShell stream type.
        /// </summary>
        public PowerShellStreamType StreamType { get; set; }

        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}