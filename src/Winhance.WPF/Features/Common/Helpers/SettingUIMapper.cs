using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Helpers
{
    /// <summary>
    /// Helper class for mapping between ApplicationSetting (Core) and SettingUIItem (WPF).
    /// Provides clean separation between business models and UI models.
    /// </summary>
    public static class SettingUIMapper
    {
        /// <summary>
        /// Creates a SettingUIItem from an ApplicationSetting.
        /// Maps only the UI-relevant properties, leaving business logic in the service layer.
        /// </summary>
        /// <param name="applicationSetting">The application setting to map from.</param>
        /// <returns>A new SettingUIItem with UI properties populated.</returns>
        public static SettingUIItem ToUIItem(ApplicationSetting applicationSetting)
        {
            var uiItem = new SettingUIItem(
                applicationSetting.Id,
                applicationSetting.Name,
                applicationSetting.Description,
                applicationSetting.GroupName)
            {
                Icon = applicationSetting.Icon,
                ControlType = applicationSetting.ControlType,
                SliderSteps = applicationSetting.SliderSteps,
                IsEnabled = applicationSetting.IsEnabled
            };

            // Set up ComboBox options if this is a ComboBox control
            if (applicationSetting.ControlType == ControlType.ComboBox)
            {
                SetupComboBoxOptions(uiItem, applicationSetting);
            }

            // Set up slider labels if this is a Slider control
            if (applicationSetting.ControlType == ControlType.Slider && applicationSetting.SliderSteps.HasValue)
            {
                SetupSliderLabels(uiItem, applicationSetting);
            }

            return uiItem;
        }

        /// <summary>
        /// Creates multiple SettingUIItems from ApplicationSettings.
        /// </summary>
        /// <param name="applicationSettings">The application settings to map from.</param>
        /// <returns>A collection of SettingUIItems.</returns>
        public static IEnumerable<SettingUIItem> ToUIItems(IEnumerable<ApplicationSetting> applicationSettings)
        {
            return applicationSettings.Select(ToUIItem);
        }

        /// <summary>
        /// Creates grouped SettingUIItems organized by GroupName.
        /// </summary>
        /// <param name="applicationSettings">The application settings to group.</param>
        /// <returns>A collection of SettingGroups with their associated SettingUIItems.</returns>
        public static IEnumerable<SettingGroup> ToGroupedUIItems(IEnumerable<ApplicationSetting> applicationSettings)
        {
            var groups = applicationSettings
                .GroupBy(s => s.GroupName)
                .Select(g => new SettingGroup(g.Key)
                {
                    Settings = new ObservableCollection<SettingUIItem>(g.Select(ToUIItem))
                });

            return groups;
        }

        /// <summary>
        /// Updates a SettingUIItem's status and values from system state.
        /// This should be called after checking the actual system state via the service.
        /// </summary>
        /// <param name="uiItem">The UI item to update.</param>
        /// <param name="isEnabled">Whether the setting is currently enabled in the system.</param>
        /// <param name="currentValue">The current value from the system.</param>
        /// <param name="status">The current status of the setting.</param>
        public static void UpdateFromSystemState(SettingUIItem uiItem, bool isEnabled, object? currentValue, RegistrySettingStatus status)
        {
            // Determine the selected value based on control type and current system state
            object? selectedValue = uiItem.ControlType switch
            {
                ControlType.BinaryToggle => isEnabled,
                ControlType.ComboBox => GetComboBoxSelectedValue(uiItem, currentValue),
                ControlType.Slider => GetSliderSelectedValue(currentValue),
                _ => currentValue
            };

            uiItem.UpdateUIStateFromSystem(isEnabled, selectedValue, status, currentValue);
        }

        #region Private Helper Methods

        private static void SetupComboBoxOptions(SettingUIItem uiItem, ApplicationSetting applicationSetting)
        {
            // Look for ComboBox options in the first registry setting's custom properties
            var registrySetting = applicationSetting.RegistrySettings?.FirstOrDefault();
            if (registrySetting?.CustomProperties?.TryGetValue("ComboBoxOptions", out var optionsObj) == true)
            {
                var options = new ObservableCollection<string>();

                // Handle both Dictionary<string, int> and Dictionary<string, object> formats
                if (optionsObj is Dictionary<string, int> intOptions)
                {
                    options = new ObservableCollection<string>(intOptions.Keys);
                }
                else if (optionsObj is Dictionary<string, object> objectOptions)
                {
                    options = new ObservableCollection<string>(objectOptions.Keys);
                }

                uiItem.ComboBoxOptions = options;
            }
        }

        private static void SetupSliderLabels(SettingUIItem uiItem, ApplicationSetting applicationSetting)
        {
            // Look for slider labels in custom properties, or create default numeric labels
            var registrySetting = applicationSetting.RegistrySettings?.FirstOrDefault();
            if (registrySetting?.CustomProperties?.TryGetValue("SliderLabels", out var labelsObj) == true &&
                labelsObj is string[] labels)
            {
                uiItem.SliderLabels = new ObservableCollection<string>(labels);
            }
            else if (applicationSetting.SliderSteps.HasValue)
            {
                // Create default numeric labels
                var defaultLabels = Enumerable.Range(0, applicationSetting.SliderSteps.Value + 1)
                    .Select(i => i.ToString())
                    .ToArray();
                uiItem.SliderLabels = new ObservableCollection<string>(defaultLabels);
            }
        }

        private static object? GetComboBoxSelectedValue(SettingUIItem uiItem, object? currentValue)
        {
            if (currentValue == null || uiItem.ComboBoxOptions.Count == 0)
                return uiItem.ComboBoxOptions.FirstOrDefault();

            // Try to find the option that corresponds to the current value
            // This is a simplified approach - in practice, you might need more sophisticated mapping
            return uiItem.ComboBoxOptions.FirstOrDefault() ?? currentValue?.ToString();
        }

        private static object? GetSliderSelectedValue(object? currentValue)
        {
            if (currentValue is int intValue)
                return intValue;
            
            if (int.TryParse(currentValue?.ToString(), out var parsedValue))
                return parsedValue;

            return 0; // Default slider value
        }

        #endregion
    }
}
