using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents the difference between a setting's current system value
    /// and the value specified in an imported config file.
    /// </summary>
    public class ConfigReviewDiff
    {
        /// <summary>
        /// The unique setting identifier.
        /// </summary>
        public string SettingId { get; set; } = string.Empty;

        /// <summary>
        /// The display name of the setting.
        /// </summary>
        public string SettingName { get; set; } = string.Empty;

        /// <summary>
        /// The feature module this setting belongs to (e.g., "privacy", "power").
        /// </summary>
        public string FeatureModuleId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable representation of the current system value.
        /// </summary>
        public string CurrentValueDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable representation of the config target value.
        /// </summary>
        public string ConfigValueDisplay { get; set; } = string.Empty;

        /// <summary>
        /// The actual config value to apply (index, bool, etc.).
        /// </summary>
        public object? ConfigValue { get; set; }

        /// <summary>
        /// The original ConfigurationItem from the config file.
        /// </summary>
        public ConfigurationItem? ConfigItem { get; set; }

        /// <summary>
        /// Whether the user has explicitly reviewed this change (made a choice).
        /// </summary>
        public bool IsReviewed { get; set; } = false;

        /// <summary>
        /// Whether the user has approved this change for application.
        /// Only meaningful when IsReviewed is true.
        /// </summary>
        public bool IsApproved { get; set; } = false;

        /// <summary>
        /// The input type of this setting (Toggle, Selection, NumericRange, etc.).
        /// </summary>
        public InputType InputType { get; set; }

        /// <summary>
        /// Whether this is a special action setting (e.g., taskbar-clean, start-menu-clean)
        /// that always requires confirmation even when no diff exists.
        /// </summary>
        public bool IsActionSetting { get; set; }

        /// <summary>
        /// Custom InfoBar message for action settings that need user confirmation.
        /// </summary>
        public string? ActionConfirmationMessage { get; set; }

        /// <summary>
        /// Raw (pre-localization) key for CurrentValueDisplay, used for re-localization on language change.
        /// For toggle settings this is "Common_On"/"Common_Off"; for combo boxes it's the raw display key
        /// (e.g. "ServiceOption_Disabled"); for power plans it's the plan's localization key.
        /// Null when no re-localization is needed (e.g. numeric values).
        /// </summary>
        public string? CurrentDisplayKey { get; set; }

        /// <summary>
        /// Raw (pre-localization) key for ConfigValueDisplay, used for re-localization on language change.
        /// </summary>
        public string? ConfigDisplayKey { get; set; }
    }
}
