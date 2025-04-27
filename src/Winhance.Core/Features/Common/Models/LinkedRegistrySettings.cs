using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents a collection of registry settings that should be treated as a single logical setting.
    /// This allows multiple registry keys to be controlled by a single toggle in the UI.
    /// </summary>
    public class LinkedRegistrySettings
    {
        /// <summary>
        /// Gets or sets the list of registry settings that are linked together.
        /// </summary>
        public List<RegistrySetting> Settings { get; set; } = new List<RegistrySetting>();

        /// <summary>
        /// Gets or sets the category for all linked settings.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description for all linked settings.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the logic to use when determining the status of linked settings.
        /// Default is Any, which means if any setting is applied, the entire setting is considered applied.
        /// </summary>
        public LinkedSettingsLogic Logic { get; set; } = LinkedSettingsLogic.Any;

        /// <summary>
        /// Creates a new instance of the LinkedRegistrySettings class.
        /// </summary>
        public LinkedRegistrySettings()
        {
        }

        /// <summary>
        /// Creates a new instance of the LinkedRegistrySettings class with a single registry setting.
        /// </summary>
        /// <param name="registrySetting">The registry setting to include.</param>
        public LinkedRegistrySettings(RegistrySetting registrySetting)
        {
            if (registrySetting != null)
            {
                Settings.Add(registrySetting);
                Category = registrySetting.Category;
                Description = registrySetting.Description;
            }
        }

        /// <summary>
        /// Creates a new instance of the LinkedRegistrySettings class with a specific logic type.
        /// </summary>
        /// <param name="logic">The logic to use when determining the status of linked settings.</param>
        public LinkedRegistrySettings(LinkedSettingsLogic logic)
        {
            Logic = logic;
        }

        /// <summary>
        /// Adds a registry setting to the linked collection.
        /// </summary>
        /// <param name="registrySetting">The registry setting to add.</param>
        public void AddSetting(RegistrySetting registrySetting)
        {
            if (registrySetting != null)
            {
                Settings.Add(registrySetting);
            }
        }
    }
}
