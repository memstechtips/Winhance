using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Microsoft.Win32;
using Winhance.Infrastructure.Features.Common.Registry;
using Winhance.WPF.Features.Common.Extensions;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for privacy optimizations.
    /// </summary>
    public partial class PrivacyOptimizationsViewModel : BaseSettingsViewModel<ApplicationSettingItem>
    {
        private readonly IDependencyManager _dependencyManager;
        private readonly IViewModelLocator? _viewModelLocator;
        private readonly ISettingsRegistry? _settingsRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivacyOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        public PrivacyOptimizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            IDependencyManager dependencyManager,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null)
            : base(progressService, registryService, logService)
        {
            _dependencyManager = dependencyManager ?? throw new ArgumentNullException(nameof(dependencyManager));
            _viewModelLocator = viewModelLocator;
            _settingsRegistry = settingsRegistry;
            _logService.Log(LogLevel.Info, "PrivacyOptimizationsViewModel instance created");
        }

        /// <summary>
        /// Loads the privacy settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, "Loading privacy settings");

                // Initialize privacy settings from PrivacyOptimizations.cs
                var privacyOptimizations = Core.Features.Optimize.Models.PrivacyOptimizations.GetPrivacyOptimizations();
                if (privacyOptimizations?.Settings != null)
                {
                    Settings.Clear();

                    // Process each setting
                    foreach (var setting in privacyOptimizations.Settings.OrderBy(s => s.Name))
                    {
                        // Create ApplicationSettingItem directly
                        var settingItem = new ApplicationSettingItem(_registryService, null, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = false, // Always initialize as unchecked
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

                        // Register the setting in the settings registry if available
                        if (_settingsRegistry != null && !string.IsNullOrEmpty(settingItem.Id))
                        {
                            _settingsRegistry.RegisterSetting(settingItem);
                            _logService.Log(LogLevel.Info, $"Registered setting {settingItem.Id} in settings registry during creation");
                        }

                        Settings.Add(settingItem);
                    }

                    // Set up property change handlers for checkboxes
                    foreach (var setting in Settings)
                    {
                        setting.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(ApplicationSettingItem.IsSelected))
                            {
                                UpdateIsSelectedState();
                            }
                        };
                    }
                }

                await CheckSettingStatusesAsync();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading privacy settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the status of all privacy settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, $"Checking status for {Settings.Count} privacy settings");

                // Clear registry caches to ensure fresh reads
                _registryService.ClearRegistryCaches();

                // Create a list of tasks to check all settings in parallel
                var tasks = Settings.Select(async setting => {
                    try
                    {
                        // Check if we have linked registry settings
                        if (setting.LinkedRegistrySettings != null && setting.LinkedRegistrySettings.Settings.Count > 0)
                        {
                            _logService.Log(LogLevel.Info, $"Checking status for linked setting: {setting.Name} with {setting.LinkedRegistrySettings.Settings.Count} registry entries");

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
                        // Fall back to single registry setting
                        else if (setting.RegistrySetting != null)
                        {
                            _logService.Log(LogLevel.Info, $"Checking status for setting: {setting.Name}");

                            // Log details about the registry entry for debugging
                            string hiveString = RegistryExtensions.GetRegistryHiveString(setting.RegistrySetting.Hive);
                            string fullPath = $"{hiveString}\\{setting.RegistrySetting.SubKey}";
                            _logService.Log(LogLevel.Info, $"Registry entry: {fullPath}\\{setting.RegistrySetting.Name}, EnabledValue={setting.RegistrySetting.EnabledValue}, DisabledValue={setting.RegistrySetting.DisabledValue}");

                            // Check if the key exists
                            bool keyExists = _registryService.KeyExists(fullPath);
                            _logService.Log(LogLevel.Info, $"Key exists: {keyExists}");

                            // Get the status
                            var status = await _registryService.GetSettingStatusAsync(setting.RegistrySetting);
                            _logService.Log(LogLevel.Info, $"Status for {setting.Name}: {status}");
                            setting.Status = status;

                            // Get the current value
                            var currentValue = await _registryService.GetCurrentValueAsync(setting.RegistrySetting);
                            _logService.Log(LogLevel.Info, $"Current value for {setting.Name}: {currentValue ?? "null"}");
                            setting.CurrentValue = currentValue;
                            
                            // Set IsRegistryValueNull for single registry setting
                            setting.IsRegistryValueNull = currentValue == null;

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
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"No registry settings available for {setting.Name}");
                            setting.Status = RegistrySettingStatus.Unknown;
                            setting.StatusMessage = "Registry setting information is missing";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Error, $"Error updating status for setting {setting.Name}: {ex.Message}");
                        setting.Status = RegistrySettingStatus.Error;
                        setting.StatusMessage = $"Error: {ex.Message}";
                    }
                }).ToList();

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);

                // Now handle any grouped settings
                foreach (var setting in Settings.Where(s => s.IsGroupedSetting && s.ChildSettings.Count > 0))
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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking privacy setting statuses: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ApplySelectedSettingsAsync and RestoreDefaultsAsync methods removed as part of the refactoring

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
