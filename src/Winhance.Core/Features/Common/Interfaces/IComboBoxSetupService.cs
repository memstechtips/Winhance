using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for setting up ComboBox options from SettingDefinitions.
    /// Handles different ComboBox data patterns and centralizes ComboBox logic.
    /// Follows SRP by focusing only on ComboBox setup responsibilities.
    /// </summary>
    public interface IComboBoxSetupService
    {
        /// <summary>
        /// Sets up ComboBox options for a setting and returns the selected value.
        /// Handles multiple data patterns: ComboBoxDisplayNames + ValueMappings.
        /// </summary>
        /// <param name="setting">The application setting containing ComboBox metadata.</param>
        /// <param name="currentValue">The current system value to match against options.</param>
        /// <returns>ComboBox setup result containing options and selected value.</returns>
        ComboBoxSetupResult SetupComboBoxOptions(SettingDefinition setting, object? currentValue);
    }

    /// <summary>
    /// Result of ComboBox setup operation containing options and selected value.
    /// </summary>
    public class ComboBoxSetupResult
    {
        /// <summary>
        /// Collection of ComboBox options for UI binding.
        /// </summary>
        public ObservableCollection<ComboBoxOption> Options { get; set; } = new();

        /// <summary>
        /// The selected value that matches the current system state.
        /// </summary>
        public object? SelectedValue { get; set; }

        /// <summary>
        /// Indicates whether ComboBox setup was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if setup failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// ComboBox option model for UI binding.
    /// </summary>
    public class ComboBoxOption
    {
        /// <summary>
        /// Text displayed to user.
        /// </summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>
        /// Value used for binding and system operations.
        /// </summary>
        public object Value { get; set; } = new();

        /// <summary>
        /// Optional description for the option.
        /// </summary>
        public string? Description { get; set; }
    }
}
