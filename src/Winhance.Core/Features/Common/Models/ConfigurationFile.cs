using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents a configuration file that stores application settings.
    /// </summary>
    public class ConfigurationFile
    {
        /// <summary>
        /// Gets or sets the type of configuration (e.g., "ExternalApps", "WindowsApps").
        /// </summary>
        public string ConfigType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the version of the configuration file format.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the date and time when the configuration file was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the collection of configuration items.
        /// </summary>
        public List<ConfigurationItem> Items { get; set; } = new List<ConfigurationItem>();
    }
}
