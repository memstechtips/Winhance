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
                IsEnabled = true, // Settings should be enabled for interaction
                IsSelected = applicationSetting.IsInitiallyEnabled // This represents the actual toggle state
            };

            // Set up ComboBox options if this is a ComboBox control
            if (applicationSetting.ControlType == ControlType.ComboBox)
            {
                SetupComboBoxOptions(uiItem, applicationSetting);
                uiItem.SelectedValue = GetComboBoxSelectedValue(uiItem, applicationSetting.CurrentValue);
            }

            // Set up slider labels if this is a Slider control
            if (applicationSetting.ControlType == ControlType.Slider && applicationSetting.SliderSteps.HasValue)
            {
                SetupSliderLabels(uiItem, applicationSetting);
            }

            // Set up tooltip properties from domain model
            SetupTooltipProperties(uiItem, applicationSetting);

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
            
            // Update tooltip properties with current system values
            UpdateTooltipCurrentValues(uiItem, currentValue);
        }

        #region Private Helper Methods

        private static void SetupComboBoxOptions(SettingUIItem uiItem, ApplicationSetting applicationSetting)
        {
            // Look for ComboBox options in the first registry setting's custom properties
            var registrySetting = applicationSetting.RegistrySettings?.FirstOrDefault();
            if (registrySetting?.CustomProperties?.TryGetValue("ComboBoxOptions", out var optionsObj) != true)
                return;

            // Extract display names from the options dictionary
            var displayNames = ExtractDisplayNames(optionsObj);
            if (displayNames.Any())
            {
                uiItem.ComboBoxOptions = new ObservableCollection<string>(displayNames);
            }
        }

        /// <summary>
        /// Extracts display names from various dictionary formats.
        /// Follows SRP by handling only the extraction logic.
        /// </summary>
        private static IEnumerable<string> ExtractDisplayNames(object optionsObj)
        {
            return optionsObj switch
            {
                Dictionary<string, int> intDict => intDict.Keys,
                Dictionary<string, object> objDict => objDict.Keys,
                System.Collections.IDictionary dict => ExtractFromGenericDictionary(dict),
                _ => Enumerable.Empty<string>()
            };
        }

        /// <summary>
        /// Extracts display names from a generic IDictionary.
        /// Uses the Value as display name if it's a string, otherwise uses the Key.
        /// </summary>
        private static IEnumerable<string> ExtractFromGenericDictionary(System.Collections.IDictionary dictionary)
        {
            var names = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var displayName = entry.Value?.ToString() ?? entry.Key?.ToString() ?? "Unknown";
                names.Add(displayName);
            }
            return names;
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
                return null;

            // Convert current value to string for comparison
            var currentValueStr = currentValue?.ToString();
            if (string.IsNullOrEmpty(currentValueStr))
                return null;

            // If currentValue is a numeric index, find the corresponding display name
            if (int.TryParse(currentValueStr, out var intValue))
            {
                // The ComboBoxOptions are ordered by the numeric values
                // So index 3 should map to the 4th option (0-based indexing)
                if (intValue >= 0 && intValue < uiItem.ComboBoxOptions.Count)
                {
                    return uiItem.ComboBoxOptions[intValue];
                }
            }

            // Try to find the exact match in ComboBoxOptions by display name
            var exactMatch = uiItem.ComboBoxOptions.FirstOrDefault(option => 
                string.Equals(option, currentValueStr, StringComparison.OrdinalIgnoreCase));
            
            if (exactMatch != null)
                return exactMatch;

            // Return the first option as fallback
            return uiItem.ComboBoxOptions.FirstOrDefault();
        }

        private static object? GetSliderSelectedValue(object? currentValue)
        {
            if (currentValue is int intValue)
                return intValue;
            
            if (int.TryParse(currentValue?.ToString(), out var parsedValue))
                return parsedValue;

            return 0; // Default slider value
        }

        /// <summary>
        /// Sets up tooltip properties from the ApplicationSetting domain model.
        /// Maintains clean architecture by exposing only the data needed for tooltip display.
        /// </summary>
        /// <param name="uiItem">The UI item to populate with tooltip data.</param>
        /// <param name="applicationSetting">The domain model containing the setting data.</param>
        private static void SetupTooltipProperties(SettingUIItem uiItem, ApplicationSetting applicationSetting)
        {
            // Set up single registry setting (for simple settings)
            if (applicationSetting.RegistrySettings.Count == 1)
            {
                uiItem.RegistrySetting = applicationSetting.RegistrySettings[0];
            }

            // Set up linked registry settings with values
            // Note: Individual current values will be populated later via UpdateTooltipData method
            uiItem.LinkedRegistrySettingsWithValues.Clear();
            foreach (var registrySetting in applicationSetting.RegistrySettings)
            {
                // Initialize with null values - will be updated when tooltip data is available
                uiItem.LinkedRegistrySettingsWithValues.Add(
                    new LinkedRegistrySettingWithValue(registrySetting, null)
                );
            }

            // Set up command settings
            uiItem.CommandSettings = new List<CommandSetting>(applicationSetting.CommandSettings);
        }

        /// <summary>
        /// Updates tooltip properties with individual registry values from tooltip data.
        /// This method uses the individual registry values retrieved during system discovery.
        /// </summary>
        /// <param name="uiItem">The UI item to update.</param>
        /// <param name="tooltipData">The tooltip data containing individual registry values.</param>
        public static void UpdateTooltipData(SettingUIItem uiItem, SettingTooltipData tooltipData)
        {
            if (tooltipData == null) 
            {
                System.Diagnostics.Debug.WriteLine($"[SettingUIMapper] UpdateTooltipData called with null tooltipData for {uiItem.Id}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[SettingUIMapper] UpdateTooltipData called for {uiItem.Id}, LinkedRegistrySettingsWithValues count: {uiItem.LinkedRegistrySettingsWithValues.Count}, IndividualRegistryValues count: {tooltipData.IndividualRegistryValues.Count}");

            // Update LinkedRegistrySettingsWithValues with actual individual registry values
            foreach (var linkedSetting in uiItem.LinkedRegistrySettingsWithValues)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingUIMapper] Checking linkedSetting for {linkedSetting.Setting.Name}");
                if (tooltipData.IndividualRegistryValues.TryGetValue(linkedSetting.Setting, out var individualValue))
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingUIMapper] Found individual value for {linkedSetting.Setting.Name}: {individualValue}, updating CurrentValue from {linkedSetting.CurrentValue}");
                    linkedSetting.CurrentValue = individualValue;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingUIMapper] No individual value found for {linkedSetting.Setting.Name}");
                }
            }
            
            // For individual registry settings, also update the main CurrentValue property
            // This is used by the single registry setting section in the tooltip template
            if (uiItem.RegistrySetting != null && tooltipData.IndividualRegistryValues.TryGetValue(uiItem.RegistrySetting, out var mainCurrentValue))
            {
                System.Diagnostics.Debug.WriteLine($"[SettingUIMapper] Updating main CurrentValue for {uiItem.Id} from {uiItem.CurrentValue} to {mainCurrentValue}");
                uiItem.CurrentValue = mainCurrentValue;
            }

            // Update command settings (they might have changed)
            uiItem.CommandSettings = new List<CommandSetting>(tooltipData.CommandSettings);
            System.Diagnostics.Debug.WriteLine($"[SettingUIMapper] UpdateTooltipData completed for {uiItem.Id}");
        }

        /// <summary>
        /// Updates the current values in tooltip properties when system state changes.
        /// This ensures tooltips show up-to-date registry values.
        /// </summary>
        /// <param name="uiItem">The UI item to update.</param>
        /// <param name="currentValue">The current value from the system.</param>
        private static void UpdateTooltipCurrentValues(SettingUIItem uiItem, object? currentValue)
        {
            // This method is kept for backward compatibility but should be replaced
            // with UpdateTooltipData for proper individual registry value handling
            foreach (var linkedSetting in uiItem.LinkedRegistrySettingsWithValues)
            {
                linkedSetting.CurrentValue = currentValue;
            }
        }

        #endregion
    }
}
