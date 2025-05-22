using System;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents a command-based setting that can be executed to enable or disable an optimization.
    /// </summary>
    public class CommandSetting
    {
        /// <summary>
        /// Gets or sets the unique identifier for this command setting.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category this command belongs to.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of what this command does.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command to execute when the setting is enabled.
        /// </summary>
        public string EnabledCommand { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command to execute when the setting is disabled.
        /// </summary>
        public string DisabledCommand { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this command requires elevation.
        /// </summary>
        public bool RequiresElevation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this is the primary command in a group.
        /// </summary>
        public bool IsPrimary { get; set; } = true;
    }
}
