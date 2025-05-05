using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Infrastructure.Features.Common.Registry;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Update optimizations.
    /// </summary>
    public partial class UpdateOptimizationsViewModel : BaseSettingsViewModel<ApplicationSettingItem>
    {
        private readonly IViewModelLocator? _viewModelLocator;
        private readonly ISettingsRegistry? _settingsRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        public UpdateOptimizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null)
            : base(progressService, registryService, logService)
        {
            _viewModelLocator = viewModelLocator;
            _settingsRegistry = settingsRegistry;
        }

        /// <summary>
        /// Loads the update settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                // Clear existing settings
                Settings.Clear();

                // Load update optimizations
                var updateOptimizations = Core.Features.Optimize.Models.UpdateOptimizations.GetUpdateOptimizations();
                if (updateOptimizations?.Settings != null)
                {
                    // Add settings sorted alphabetically by name
                    foreach (var setting in updateOptimizations.Settings.OrderBy(s => s.Name))
                    {
                        // Create ApplicationSettingItem directly
                        var settingItem = new ApplicationSettingItem(_registryService, null, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsUpdatingFromCode = true, // Set this to true to allow RefreshStatus to set the correct state
                            GroupName = setting.GroupName,
                            Dependencies = setting.Dependencies,
                            ControlType = ControlType.BinaryToggle // Default to binary toggle
                        };

                        // Set up the registry settings
                        if (setting.RegistrySettings.Count == 1)
                        {
                            // Single registry setting
                            settingItem.RegistrySetting = setting.RegistrySettings[0];
                            _logService.Log(LogLevel.Info, $"Setting up single registry setting for {setting.Name}: {setting.RegistrySettings[0].Hive}\\{setting.RegistrySettings[0].SubKey}\\{setting.RegistrySettings[0].Name}");
                        }
                        else if (setting.RegistrySettings.Count > 1)
                        {
                            // Linked registry settings
                            settingItem.LinkedRegistrySettings = setting.CreateLinkedRegistrySettings();
                            _logService.Log(LogLevel.Info, $"Setting up linked registry settings for {setting.Name} with {setting.RegistrySettings.Count} entries and logic {setting.LinkedSettingsLogic}");
                            
                            // Log details about each registry entry for debugging
                            foreach (var regSetting in setting.RegistrySettings)
                            {
                                _logService.Log(LogLevel.Info, $"Linked registry entry: {regSetting.Hive}\\{regSetting.SubKey}\\{regSetting.Name}, IsPrimary={regSetting.IsPrimary}");
                            }
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"No registry settings found for {setting.Name}");
                        }

                        // Add to the settings collection
                        Settings.Add(settingItem);
                    }

                    // Register settings with the settings registry if available
                    foreach (var setting in Settings)
                    {
                        if (_settingsRegistry != null && !string.IsNullOrEmpty(setting.Id))
                        {
                            _settingsRegistry.RegisterSetting(setting);
                            _logService.Log(LogLevel.Info, $"Registered setting {setting.Id} in settings registry during creation");
                        }
                    }

                    // Refresh status for all settings to populate LinkedRegistrySettingsWithValues
                    foreach (var setting in Settings)
                    {
                        await setting.RefreshStatus();
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading update optimizations: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the status of all Windows Update settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, $"Checking status for {Settings.Count} Windows Update settings");

                foreach (var setting in Settings)
                {
                    try
                    {
                        if (setting.RegistrySetting != null)
                        {
                            _logService.Log(LogLevel.Info, $"Checking status for setting: {setting.Name}");

                            // Get the status
                            var status = await _registryService.GetSettingStatusAsync(setting.RegistrySetting);
                            _logService.Log(LogLevel.Info, $"Status for {setting.Name}: {status}");
                            setting.Status = status;

                            // Get the current value
                            var currentValue = await _registryService.GetCurrentValueAsync(setting.RegistrySetting);
                            _logService.Log(LogLevel.Info, $"Current value for {setting.Name}: {currentValue ?? "null"}");
                            setting.CurrentValue = currentValue;

                            // Add to LinkedRegistrySettingsWithValues for tooltip display
                            setting.LinkedRegistrySettingsWithValues.Clear();
                            setting.LinkedRegistrySettingsWithValues.Add(new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(setting.RegistrySetting, currentValue));

                            // Set status message
                            setting.StatusMessage = GetStatusMessage(setting);

                            // Update IsSelected based on status
                            bool shouldBeSelected = status == RegistrySettingStatus.Applied;

                            // Set the checkbox state to match the registry state
                            _logService.Log(LogLevel.Info, $"Setting {setting.Name} status is {status}, setting IsSelected to {shouldBeSelected}");
                            setting.IsUpdatingFromCode = true;
                            try
                            {
                                setting.IsSelected = shouldBeSelected;
                            }
                            finally
                            {
                                setting.IsUpdatingFromCode = false;
                            }
                        }
                        else if (setting.LinkedRegistrySettings != null && setting.LinkedRegistrySettings.Settings.Count > 0)
                        {
                            _logService.Log(LogLevel.Info, $"Checking linked registry settings status for: {setting.Name} with {setting.LinkedRegistrySettings.Settings.Count} registry entries");

                            // Log details about each registry entry for debugging
                            foreach (var regSetting in setting.LinkedRegistrySettings.Settings)
                            {
                                string hiveString = RegistryExtensions.GetRegistryHiveString(regSetting.Hive);
                                string fullPath = $"{hiveString}\\{regSetting.SubKey}";
                                _logService.Log(LogLevel.Info, $"Registry entry: {fullPath}\\{regSetting.Name}, EnabledValue={regSetting.EnabledValue}, DisabledValue={regSetting.DisabledValue}");

                                // Check if the key exists
                                bool keyExists = _registryService.KeyExists(fullPath);
                                _logService.Log(LogLevel.Info, $"Key exists: {keyExists}");

                                if (keyExists)
                                {
                                    // Check if the value exists and get its current value
                                    var currentValue = await _registryService.GetCurrentValueAsync(regSetting);
                                    _logService.Log(LogLevel.Info, $"Current value: {currentValue ?? "null"}");
                                }
                            }

                            // Get the combined status of all linked settings
                            var status = await _registryService.GetLinkedSettingsStatusAsync(setting.LinkedRegistrySettings);
                            _logService.Log(LogLevel.Info, $"Combined status for {setting.Name}: {status}");
                            setting.Status = status;

                            // For current value display, use the first setting's value
                            if (setting.LinkedRegistrySettings.Settings.Count > 0)
                            {
                                var firstSetting = setting.LinkedRegistrySettings.Settings[0];
                                var currentValue = await _registryService.GetCurrentValueAsync(firstSetting);
                                _logService.Log(LogLevel.Info, $"Current value for {setting.Name} (first entry): {currentValue ?? "null"}");
                                setting.CurrentValue = currentValue;

                                // Check for null registry values
                                bool anyNull = false;

                                // Populate the LinkedRegistrySettingsWithValues collection for tooltip display
                                setting.LinkedRegistrySettingsWithValues.Clear();
                                foreach (var regSetting in setting.LinkedRegistrySettings.Settings)
                                {
                                    var regCurrentValue = await _registryService.GetCurrentValueAsync(regSetting);
                                    _logService.Log(LogLevel.Info, $"Current value for linked setting {regSetting.Name}: {regCurrentValue ?? "null"}");
                                    
                                    if (regCurrentValue == null)
                                    {
                                        anyNull = true;
                                    }
                                    
                                    setting.LinkedRegistrySettingsWithValues.Add(new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(regSetting, regCurrentValue));
                                }
                                
                                // Set IsRegistryValueNull for linked settings
                                setting.IsRegistryValueNull = anyNull;
                            }

                            // Set status message
                            setting.StatusMessage = GetStatusMessage(setting);

                            // Update IsSelected based on status
                            bool shouldBeSelected = status == RegistrySettingStatus.Applied;

                            // Set the checkbox state to match the registry state
                            _logService.Log(LogLevel.Info, $"Setting {setting.Name} status is {status}, setting IsSelected to {shouldBeSelected}");
                            setting.IsUpdatingFromCode = true;
                            try
                            {
                                setting.IsSelected = shouldBeSelected;
                            }
                            finally
                            {
                                setting.IsUpdatingFromCode = false;
                            }
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"Registry setting is null for {setting.Name}");
                            setting.Status = RegistrySettingStatus.Unknown;
                            setting.StatusMessage = "Registry setting information is missing";
                        }

                        // If this is a grouped setting, update child settings too
                        if (setting.IsGroupedSetting && setting.ChildSettings.Count > 0)
                        {
                            _logService.Log(LogLevel.Info, $"Updating {setting.ChildSettings.Count} child settings for {setting.Name}");
                            foreach (var childSetting in setting.ChildSettings)
                            {
                                if (childSetting.RegistrySetting != null)
                                {
                                    var status = await _registryService.GetSettingStatusAsync(childSetting.RegistrySetting);
                                    childSetting.Status = status;

                                    var currentValue = await _registryService.GetCurrentValueAsync(childSetting.RegistrySetting);
                                    childSetting.CurrentValue = currentValue;

                                    childSetting.StatusMessage = GetStatusMessage(childSetting);

                                    // Update IsSelected based on status
                                    bool shouldBeSelected = status == RegistrySettingStatus.Applied;

                                    // Set the checkbox state to match the registry state
                                    _logService.Log(LogLevel.Info, $"Child setting {childSetting.Name} status is {status}, setting IsSelected to {shouldBeSelected}");
                                    childSetting.IsUpdatingFromCode = true;
                                    try
                                    {
                                        childSetting.IsSelected = shouldBeSelected;
                                    }
                                    finally
                                    {
                                        childSetting.IsUpdatingFromCode = false;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Error, $"Error updating status for setting {setting.Name}: {ex.Message}");
                        setting.Status = RegistrySettingStatus.Error;
                        setting.StatusMessage = $"Error: {ex.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking Windows Update setting statuses: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Updates the IsSelected state based on individual selections.
        /// </summary>
        private void UpdateIsSelectedState()
        {
            if (Settings.Count == 0)
                return;

            var selectedCount = Settings.Count(setting => setting.IsSelected);
            IsSelected = selectedCount == Settings.Count;
        }

        /// <summary>
        /// Gets a user-friendly status message for a setting.
        /// </summary>
        /// <param name="setting">The setting to get the status message for.</param>
        /// <returns>A user-friendly status message.</returns>
        private string GetStatusMessage(ApplicationSettingItem setting)
        {
            return setting.Status switch
            {
                RegistrySettingStatus.Applied =>
                    "This setting is enabled (toggle is ON).",

                RegistrySettingStatus.NotApplied =>
                    setting.CurrentValue == null
                        ? "This setting is not applied (registry value does not exist)."
                        : "This setting is disabled (toggle is OFF).",

                RegistrySettingStatus.Modified =>
                    "This setting has a custom value that differs from both enabled and disabled values.",

                RegistrySettingStatus.Error =>
                    "An error occurred while checking this setting's status.",

                _ => "The status of this setting is unknown."
            };
        }
    }
}
