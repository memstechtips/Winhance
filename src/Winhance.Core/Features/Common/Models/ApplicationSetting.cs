using Microsoft.Win32;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Base class for all application settings that can modify registry values.
    /// </summary>
    public abstract record ApplicationSetting
    {
        /// <summary>
        /// Gets or sets the unique identifier for this setting.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets or sets the user-friendly name for this setting.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the description for this setting.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Gets or sets the group name for this setting.
        /// </summary>
        public required string GroupName { get; init; }

        /// <summary>
        /// Gets or sets the collection of registry settings associated with this application setting.
        /// </summary>
        public List<RegistrySetting> RegistrySettings { get; init; } = new List<RegistrySetting>();

        /// <summary>
        /// Gets or sets the collection of command settings associated with this application setting.
        /// </summary>
        public List<CommandSetting> CommandSettings { get; init; } = new List<CommandSetting>();

        /// <summary>
        /// Gets or sets the logic to use when determining the status of linked registry settings.
        /// </summary>
        public LinkedSettingsLogic LinkedSettingsLogic { get; init; } = LinkedSettingsLogic.Any;

        /// <summary>
        /// Gets or sets the dependencies between settings.
        /// </summary>
        public List<SettingDependency> Dependencies { get; init; } = new List<SettingDependency>();

        /// <summary>
        /// Gets or sets the control type for the UI.
        /// </summary>
        public ControlType ControlType { get; init; } = ControlType.BinaryToggle;

        /// <summary>
        /// Gets or sets the number of steps for a slider control.
        /// </summary>
        public int? SliderSteps { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether this setting is enabled.
        /// </summary>
        public bool IsEnabled { get; init; }

        /// <summary>
        /// Gets or sets the icon for this setting (Material Symbols font character).
        /// </summary>
        public string? Icon { get; init; }

        /// <summary>
        /// Creates a LinkedRegistrySettings object from the RegistrySettings collection.
        /// </summary>
        /// <returns>A LinkedRegistrySettings object.</returns>
        public LinkedRegistrySettings CreateLinkedRegistrySettings()
        {
            if (RegistrySettings.Count == 0)
            {
                return new LinkedRegistrySettings();
            }

            var linkedSettings = new LinkedRegistrySettings
            {
                Category = RegistrySettings[0].Category,
                Description = Description,
                Logic = LinkedSettingsLogic
            };

            foreach (var registrySetting in RegistrySettings)
            {
                linkedSettings.AddSetting(registrySetting);
            }

            return linkedSettings;
        }
    }
}
