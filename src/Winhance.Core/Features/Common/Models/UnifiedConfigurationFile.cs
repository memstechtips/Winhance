using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents a unified configuration file that stores settings for multiple parts of the application.
    /// </summary>
    public class UnifiedConfigurationFile
    {
        /// <summary>
        /// Gets or sets the version of the configuration file format.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the date and time when the configuration file was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the Windows Apps configuration section.
        /// </summary>
        public ConfigSection WindowsApps { get; set; } = new ConfigSection();

        /// <summary>
        /// Gets or sets the External Apps configuration section.
        /// </summary>
        public ConfigSection ExternalApps { get; set; } = new ConfigSection();

        /// <summary>
        /// Gets or sets the Customize configuration section.
        /// </summary>
        public ConfigSection Customize { get; set; } = new ConfigSection();

        /// <summary>
        /// Gets or sets the Optimize configuration section.
        /// </summary>
        public ConfigSection Optimize { get; set; } = new ConfigSection();
    }

    /// <summary>
    /// Represents a section in the unified configuration file.
    /// </summary>
    public class ConfigSection
    {
        /// <summary>
        /// Gets or sets a value indicating whether this section is included in the configuration.
        /// </summary>
        public bool IsIncluded { get; set; } = false;

        /// <summary>
        /// Gets or sets the collection of configuration items in this section.
        /// </summary>
        public List<ConfigurationItem> Items { get; set; } = new List<ConfigurationItem>();

        /// <summary>
        /// Gets or sets the description of this section.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the icon for this section.
        /// </summary>
        public string Icon { get; set; }
    }
}