using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Optimize.Models
{

    /// <summary>
    /// Represents a power setting subgroup.
    /// </summary>
    public class PowerSettingSubgroup
    {
        /// <summary>
        /// Gets or sets the unique identifier for the subgroup.
        /// </summary>
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the alias for the subgroup GUID.
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name for the subgroup.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of power settings in this subgroup.
        /// </summary>
        public List<PowerSettingDefinition> Settings { get; set; } = new List<PowerSettingDefinition>();
    }

    /// <summary>
    /// Represents a power setting definition.
    /// </summary>
    public class PowerSettingDefinition
    {
        /// <summary>
        /// Gets or sets the unique identifier for the setting.
        /// </summary>
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the alias for the setting GUID.
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name for the setting.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description for the setting.
        /// </summary>
        public string Description { get; set; } = string.Empty;


        /// <summary>
        /// Gets or sets the parent subgroup GUID.
        /// </summary>
        public string SubgroupGuid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the minimum possible value for numeric settings.
        /// </summary>
        public int MinValue { get; set; }

        /// <summary>
        /// Gets or sets the maximum possible value for numeric settings.
        /// </summary>
        public int MaxValue { get; set; }

        /// <summary>
        /// Gets or sets the increment for numeric settings.
        /// </summary>
        public int Increment { get; set; } = 1;

        /// <summary>
        /// Gets or sets the units for numeric settings (e.g., "Seconds", "%").
        /// </summary>
        public string Units { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this setting should use predefined time intervals.
        /// </summary>
        public bool UseTimeIntervals { get; set; }

        /// <summary>
        /// Gets or sets the predefined time values for settings that use time intervals.
        /// </summary>
        public List<PowerSettingTimeValue> TimeValues { get; set; } = new List<PowerSettingTimeValue>();

        /// <summary>
        /// Gets or sets the possible values for enum settings.
        /// </summary>
        public List<PowerSettingValue> PossibleValues { get; set; } = new List<PowerSettingValue>();

        /// <summary>
        /// Gets or sets the current AC power setting index.
        /// </summary>
        public int CurrentAcValue { get; set; }

        /// <summary>
        /// Gets or sets the current DC power setting index.
        /// </summary>
        public int CurrentDcValue { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this setting uses a custom command instead of powercfg.
        /// </summary>
        public bool CustomCommand { get; set; } = false;

        /// <summary>
        /// Gets or sets the template for the custom command.
        /// </summary>
        public string CustomCommandTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a mapping from index values to command arguments.
        /// </summary>
        public Dictionary<int, string> CustomCommandValueMap { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// Gets or sets the input type to use in the UI for this setting.
        /// </summary>
        public SettingInputType InputType { get; set; } = SettingInputType.NumericRange;
    }

    /// <summary>
    /// Represents a possible value for an enum power setting.
    /// </summary>
    public class PowerSettingValue
    {
        /// <summary>
        /// Gets or sets the index of the setting value.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the friendly name of the setting value.
        /// </summary>
        public string FriendlyName { get; set; } = string.Empty;
    }
}
