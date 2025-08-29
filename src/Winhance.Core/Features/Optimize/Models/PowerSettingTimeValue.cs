using System;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents a time value option for power settings that use time intervals.
    /// </summary>
    public class PowerSettingTimeValue
    {
        /// <summary>
        /// Gets or sets the time value in minutes.
        /// </summary>
        public int Minutes { get; set; }

        /// <summary>
        /// Gets or sets the display name for the time value.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
    }
}
