namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Represents an option for ComboBox controls in settings UI.
    /// Pure UI model with no business logic.
    /// </summary>
    public class SettingOption
    {
        /// <summary>
        /// The actual value that will be used when this option is selected.
        /// </summary>
        public required string Value { get; init; }

        /// <summary>
        /// The text displayed to the user for this option.
        /// </summary>
        public required string DisplayText { get; init; }

        /// <summary>
        /// Optional description or tooltip text for this option.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Whether this option is currently enabled/selectable.
        /// </summary>
        public bool IsEnabled { get; init; } = true;

        /// <summary>
        /// Optional icon for this option (Material Symbols font character).
        /// </summary>
        public string? Icon { get; init; }

        /// <summary>
        /// Constructor for creating a setting option.
        /// </summary>
        /// <param name="value">The value for this option.</param>
        /// <param name="displayText">The display text for this option.</param>
        public SettingOption(string value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }

        /// <summary>
        /// Constructor with description.
        /// </summary>
        /// <param name="value">The value for this option.</param>
        /// <param name="displayText">The display text for this option.</param>
        /// <param name="description">Optional description for this option.</param>
        public SettingOption(string value, string displayText, string? description)
        {
            Value = value;
            DisplayText = displayText;
            Description = description;
        }

        public override string ToString() => DisplayText;
    }
}
