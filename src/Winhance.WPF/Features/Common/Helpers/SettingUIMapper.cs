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
            };

            // Set up NumericUpDown properties BEFORE setting the value to prevent clamping
            if (applicationSetting.ControlType == ControlType.NumericUpDown)
            {
                SetupNumericUpDownProperties(uiItem, applicationSetting);
            }

            // Set up ComboBox options if this is a ComboBox control
            if (applicationSetting.ControlType == ControlType.ComboBox)
            {
                SetupComboBoxOptions(uiItem, applicationSetting);
                // Use UpdateUIStateFromSystem to prevent triggering delegates during initialization
                var selectedValue = GetComboBoxSelectedValue(uiItem, applicationSetting.CurrentValue);
                uiItem.UpdateUIStateFromSystem(applicationSetting.IsInitiallyEnabled, selectedValue, RegistrySettingStatus.Unknown, applicationSetting.CurrentValue);
            }
            else
            {
                // For non-ComboBox controls, set IsSelected normally
                uiItem.UpdateUIStateFromSystem(applicationSetting.IsInitiallyEnabled, null, RegistrySettingStatus.Unknown, applicationSetting.CurrentValue);
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
        /// Creates grouped SettingUIItems organized by GroupName using existing UI items.
        /// </summary>
        /// <param name="uiItems">The existing UI items to group.</param>
        /// <returns>A collection of SettingGroups with their associated SettingUIItems.</returns>
        public static IEnumerable<SettingGroup> ToGroupedUIItems(IEnumerable<SettingUIItem> uiItems)
        {
            var groups = uiItems
                .GroupBy(s => s.GroupName)
                .Select(g => new SettingGroup(g.Key)
                {
                    Settings = new ObservableCollection<SettingUIItem>(g)
                });

            return groups;
        }

        /// <summary>
        /// Updates a SettingUIItem's status and values from system state.
        /// This should be called after checking the actual system state via the service.
        /// </summary>
        /// <param name="uiItem">The UI item to update.</param>
        /// <param name="isSelected">Whether the setting is currently selected/enabled.</param>
        /// <param name="currentValue">The current value from the system.</param>
        /// <param name="status">The current status of the setting.</param>
        public static void UpdateFromSystemState(SettingUIItem uiItem, bool isSelected, object? currentValue, RegistrySettingStatus status)
        {
            // Use the safe update method that prevents triggering delegates during system state updates
            uiItem.UpdateUIStateFromSystem(isSelected, null, status, currentValue);
            
            // Update tooltip properties with current system values
            UpdateTooltipCurrentValues(uiItem, currentValue);
        }

        /// <summary>
        /// Updates a SettingUIItem from a refreshed ApplicationSetting during refresh operations.
        /// CRITICAL: Ensures NumericUpDown constraints are updated BEFORE setting current values
        /// to prevent the "Turn off hard disk" 100-minute capping bug during navigation refresh.
        /// Follows SOLID principles by delegating to specialized setup methods.
        /// </summary>
        /// <param name="uiItem">The existing UI item to update.</param>
        /// <param name="applicationSetting">The refreshed application setting with updated properties.</param>
        public static void UpdateFromRefreshedApplicationSetting(SettingUIItem uiItem, ApplicationSetting applicationSetting)
        {
            // CRITICAL FIX: For NumericUpDown controls, update constraint properties FIRST
            // This prevents the default Maximum=100 from capping values during refresh cycles
            if (applicationSetting.ControlType == ControlType.NumericUpDown)
            {
                SetupNumericUpDownProperties(uiItem, applicationSetting);
            }

            // Update ComboBox options if they may have changed
            if (applicationSetting.ControlType == ControlType.ComboBox)
            {
                SetupComboBoxOptions(uiItem, applicationSetting);
                var selectedValue = GetComboBoxSelectedValue(uiItem, applicationSetting.CurrentValue);
                uiItem.UpdateUIStateFromSystem(applicationSetting.IsInitiallyEnabled, selectedValue, RegistrySettingStatus.Unknown, applicationSetting.CurrentValue);
            }
            else
            {
                // For other controls, update normally
                uiItem.UpdateUIStateFromSystem(applicationSetting.IsInitiallyEnabled, null, RegistrySettingStatus.Unknown, applicationSetting.CurrentValue);
            }

            // Update other properties that may have changed
            uiItem.Name = applicationSetting.Name;
            uiItem.Description = applicationSetting.Description;
            uiItem.IsEnabled = true;

            // Update tooltip properties from refreshed domain model
            SetupTooltipProperties(uiItem, applicationSetting);
        }

        #region Private Helper Methods

        private static void SetupComboBoxOptions(SettingUIItem uiItem, ApplicationSetting applicationSetting)
        {
            object? optionsObj = null;

            // First, try to get ComboBox options from registry settings (for registry-based settings)
            var registrySetting = applicationSetting.RegistrySettings?.FirstOrDefault();
            if (registrySetting?.CustomProperties?.TryGetValue("ComboBoxOptions", out optionsObj) == true)
            {
                // Registry settings use string-based options
                var displayNames = ExtractDisplayNames(optionsObj);
                if (displayNames.Any())
                {
                    uiItem.ComboBoxOptions = new ObservableCollection<string>(displayNames);
                }
            }
            // Check for ComboBoxDisplayNames in ApplicationSetting.CustomProperties (for ValueMappings-based settings)
            else if (applicationSetting.CustomProperties?.TryGetValue("ComboBoxDisplayNames", out optionsObj) == true)
            {
                // ValueMappings settings use ComboBoxDisplayNames array
                if (optionsObj is string[] displayNamesArray)
                {
                    uiItem.ComboBoxOptions = new ObservableCollection<string>(displayNamesArray);
                }
                else
                {
                    // Fallback: extract display names from ComboBoxDisplayNames in other formats
                    var displayNames = ExtractDisplayNames(optionsObj);
                    if (displayNames.Any())
                    {
                        uiItem.ComboBoxOptions = new ObservableCollection<string>(displayNames);
                    }
                }
            }
            // For power settings and other command-based settings, check ApplicationSetting.CustomProperties
            else if (applicationSetting.CustomProperties?.TryGetValue("Options", out optionsObj) == true)
            {
                // Power settings use object-based options with Name and Value properties
                var optionObjects = ExtractOptionObjects(optionsObj);
                if (optionObjects.Any())
                {
                    uiItem.Options = new ObservableCollection<object>(optionObjects);
                }
            }
        }

        /// <summary>
        /// Sets up NumericUpDown control properties from the ApplicationSetting.
        /// Extracts MinValue, MaxValue, Increment, and Units from CustomProperties.
        /// </summary>
        private static void SetupNumericUpDownProperties(SettingUIItem uiItem, ApplicationSetting applicationSetting)
        {
            if (applicationSetting.CustomProperties != null)
            {
                // Set MinValue
                if (applicationSetting.CustomProperties.TryGetValue("MinValue", out var minValueObj) && 
                    int.TryParse(minValueObj?.ToString(), out var minValue))
                {
                    uiItem.MinValue = minValue;
                }

                // Set MaxValue
                if (applicationSetting.CustomProperties.TryGetValue("MaxValue", out var maxValueObj) && 
                    int.TryParse(maxValueObj?.ToString(), out var maxValue))
                {
                    uiItem.MaxValue = maxValue;
                }

                // Set Units
                if (applicationSetting.CustomProperties.TryGetValue("Units", out var unitsObj))
                {
                    uiItem.Units = unitsObj?.ToString() ?? string.Empty;
                }
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
                Dictionary<string, int> intDict => intDict.Keys.OrderBy(k => k), // Consistent alphabetical ordering
                Dictionary<string, object> objDict => objDict.Keys.OrderBy(k => k), // Consistent alphabetical ordering
                System.Collections.IDictionary dict => ExtractFromGenericDictionary(dict),
                // Handle power settings format: List of objects with Name and Value properties
                // Note: Put this before generic IEnumerable to avoid conflicts with IDictionary
                List<object> objectList => ExtractFromObjectList(objectList),
                System.Collections.IList list => ExtractFromObjectList(list),
                System.Collections.IEnumerable enumerable when !(enumerable is string) => ExtractFromObjectList(enumerable),
                _ => Enumerable.Empty<string>()
            };
        }

        /// <summary>
        /// Extracts option objects with Name and Value properties for XAML binding.
        /// This transforms power setting data into the format expected by ComboBox XAML.
        /// </summary>
        private static IEnumerable<object> ExtractOptionObjects(object optionsObj)
        {
            var options = new List<object>();
            
            if (optionsObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        // If it's already a PowerSettingOption, use it directly
                        if (item.GetType().Name == "PowerSettingOption")
                        {
                            options.Add(item);
                            continue;
                        }
                        
                        var type = item.GetType();
                        
                        // Try to get FriendlyName and Index properties (power settings format)
                        var friendlyNameProperty = type.GetProperty("FriendlyName");
                        var indexProperty = type.GetProperty("Index");
                        
                        if (friendlyNameProperty != null && indexProperty != null)
                        {
                            var name = friendlyNameProperty.GetValue(item)?.ToString();
                            var value = indexProperty.GetValue(item);
                            
                            if (!string.IsNullOrEmpty(name) && value != null)
                            {
                                // Create anonymous object with Name and Value properties for XAML binding
                                options.Add(new { Name = name, Value = value });
                            }
                        }
                        // Try to get Name and Value properties (anonymous objects format)
                        else
                        {
                            var nameProperty = type.GetProperty("Name");
                            var valueProperty = type.GetProperty("Value");
                            
                            if (nameProperty != null && valueProperty != null)
                            {
                                var name = nameProperty.GetValue(item)?.ToString();
                                var value = valueProperty.GetValue(item);
                                
                                if (!string.IsNullOrEmpty(name) && value != null)
                                {
                                    options.Add(new { Name = name, Value = value });
                                }
                            }
                        }
                    }
                }
            }
            
            return options;
        }

        /// <summary>
        /// Extracts display names from a list of objects with Name and Value properties.
        /// This handles the power settings format where options are stored as anonymous objects.
        /// </summary>
        private static IEnumerable<string> ExtractFromObjectList(System.Collections.IEnumerable enumerable)
        {
            var names = new List<string>();
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    // Use reflection to get the display name property
                    var type = item.GetType();
                    string? nameValue = null;
                    
                    // Try different property names: FriendlyName (power settings), Name (anonymous objects)
                    var friendlyNameProperty = type.GetProperty("FriendlyName");
                    if (friendlyNameProperty != null)
                    {
                        nameValue = friendlyNameProperty.GetValue(item)?.ToString();
                    }
                    else
                    {
                        var nameProperty = type.GetProperty("Name");
                        if (nameProperty != null)
                        {
                            nameValue = nameProperty.GetValue(item)?.ToString();
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(nameValue))
                    {
                        names.Add(nameValue);
                    }
                }
            }
            return names;
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
            // Power settings use Options (object collection), registry settings use ComboBoxOptions (string collection)
            
            // Handle power settings with Options (Name/Value pairs)
            if (uiItem.Options?.Count > 0)
            {
                if (currentValue == null)
                    return null;

                // Convert current value to integer for power settings
                if (int.TryParse(currentValue.ToString(), out var intValue))
                {
                    // Find the option with matching Value property
                    var matchingOption = uiItem.Options.FirstOrDefault(option =>
                    {
                        var valueProperty = option.GetType().GetProperty("Value");
                        if (valueProperty != null)
                        {
                            var optionValue = valueProperty.GetValue(option);
                            return optionValue != null && optionValue.Equals(intValue);
                        }
                        return false;
                    });

                    if (matchingOption != null)
                    {
                        // Set the SelectedOption for SelectedItem binding
                        uiItem.SelectedOption = matchingOption;
                        
                        // Also return the Value property for backward compatibility with SelectedValue binding
                        var valueProperty = matchingOption.GetType().GetProperty("Value");
                        return valueProperty?.GetValue(matchingOption);
                    }
                }
                
                // Fallback: return the value of the first option
                var firstOption = uiItem.Options.FirstOrDefault();
                if (firstOption != null)
                {
                    // Set the SelectedOption for SelectedItem binding
                    uiItem.SelectedOption = firstOption;
                    
                    var valueProperty = firstOption.GetType().GetProperty("Value");
                    return valueProperty?.GetValue(firstOption);
                }
                
                return null;
            }
            
            // Handle registry settings with ComboBoxOptions (string collection) - LEGACY CODE
            if (uiItem.ComboBoxOptions?.Count > 0)
            {
                if (currentValue == null)
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
            
            return null;
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
                return;
            }

            // Update LinkedRegistrySettingsWithValues with actual individual registry values
            foreach (var linkedSetting in uiItem.LinkedRegistrySettingsWithValues)
            {
                if (tooltipData.IndividualRegistryValues.TryGetValue(linkedSetting.Setting, out var individualValue))
                {
                    linkedSetting.CurrentValue = individualValue;
                }
                else
                {
                }
            }
            
            // For individual registry settings, also update the main CurrentValue property
            // This is used by the single registry setting section in the tooltip template
            if (uiItem.RegistrySetting != null && tooltipData.IndividualRegistryValues.TryGetValue(uiItem.RegistrySetting, out var mainCurrentValue))
            {
                uiItem.CurrentValue = mainCurrentValue;
            }

            // Update command settings (they might have changed)
            uiItem.CommandSettings = new List<CommandSetting>(tooltipData.CommandSettings);
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
