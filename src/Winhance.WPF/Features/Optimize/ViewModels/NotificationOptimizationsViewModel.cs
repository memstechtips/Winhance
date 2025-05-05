using System;
using System.Collections.Generic;
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

using Winhance.WPF.Features.Common.Extensions;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Notifications optimizations.
    /// </summary>
    public class NotificationOptimizationsViewModel : BaseSettingsViewModel<ApplicationSettingItem>
    {
        private readonly IDialogService _dialogService;
        private readonly IDependencyManager _dependencyManager;
        private readonly IViewModelLocator? _viewModelLocator;
        private readonly ISettingsRegistry? _settingsRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        public NotificationOptimizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            IDialogService dialogService,
            IDependencyManager dependencyManager,
            IViewModelLocator? viewModelLocator = null,
            ISettingsRegistry? settingsRegistry = null)
            : base(progressService, registryService, logService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _dependencyManager = dependencyManager ?? throw new ArgumentNullException(nameof(dependencyManager));
            _viewModelLocator = viewModelLocator;
            _settingsRegistry = settingsRegistry;
            _logService.Log(LogLevel.Info, "NotificationOptimizationsViewModel instance created");
        }

        /// <summary>
        /// Loads the Notifications settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, "NotificationOptimizationsViewModel.LoadSettingsAsync: Starting to load notification optimizations");

                // Clear existing settings
                Settings.Clear();

                // Load Notification optimizations from NotificationOptimizations
                var notificationOptimizations = Core.Features.Optimize.Models.NotificationOptimizations.GetNotificationOptimizations();
                _logService.Log(LogLevel.Info, $"NotificationOptimizationsViewModel.LoadSettingsAsync: Got {notificationOptimizations?.Settings?.Count ?? 0} notification optimizations");
                if (notificationOptimizations?.Settings != null)
                {
                    // Add settings sorted alphabetically by name
                    foreach (var setting in notificationOptimizations.Settings.OrderBy(s => s.Name))
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
                        _logService.Log(LogLevel.Info, $"NotificationOptimizationsViewModel.LoadSettingsAsync: Added setting {setting.Name} to collection");
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

                    _logService.Log(LogLevel.Info, $"NotificationOptimizationsViewModel.LoadSettingsAsync: Added {Settings.Count} settings to collection");
                }

                // Check setting statuses
                await CheckSettingStatusesAsync();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading notification settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the status of all Notifications settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, $"NotificationOptimizationsViewModel.CheckSettingStatusesAsync: Checking status for {Settings.Count} settings");

                foreach (var setting in Settings)
                {
                    if (setting.RegistrySetting != null)
                    {
                        // Get status
                        var status = await _registryService.GetSettingStatusAsync(setting.RegistrySetting);
                        setting.Status = status;

                        // Get current value
                        var currentValue = await _registryService.GetCurrentValueAsync(setting.RegistrySetting);
                        setting.CurrentValue = currentValue;
                        
                        // Set IsRegistryValueNull property based on current value
                        setting.IsRegistryValueNull = currentValue == null;

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
                        // Get the combined status of all linked settings
                        var status = await _registryService.GetLinkedSettingsStatusAsync(setting.LinkedRegistrySettings);
                        setting.Status = status;

                        // For current value display, use the first setting's value
                        if (setting.LinkedRegistrySettings.Settings.Count > 0)
                        {
                            var firstSetting = setting.LinkedRegistrySettings.Settings[0];
                            var currentValue = await _registryService.GetCurrentValueAsync(firstSetting);
                            setting.CurrentValue = currentValue;

                            // Check for null registry values
                            bool anyNull = false;

                            // Populate the LinkedRegistrySettingsWithValues collection for tooltip display
                            setting.LinkedRegistrySettingsWithValues.Clear();
                            foreach (var regSetting in setting.LinkedRegistrySettings.Settings)
                            {
                                var regCurrentValue = await _registryService.GetCurrentValueAsync(regSetting);
                                
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
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking notification setting statuses: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the status message for a setting.
        /// </summary>
        /// <param name="setting">The setting.</param>
        /// <returns>The status message.</returns>
        private string GetStatusMessage(ApplicationSettingItem setting)
        {
            return setting.Status switch
            {
                RegistrySettingStatus.Applied => "Applied",
                RegistrySettingStatus.NotApplied => "Not Applied",
                RegistrySettingStatus.Error => "Error",
                RegistrySettingStatus.Unknown => "Unknown",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Updates the IsSelected state based on individual selections.
        /// </summary>
        private void UpdateIsSelectedState()
        {
            if (Settings.Count == 0) return;

            bool allSelected = Settings.All(s => s.IsSelected);
            bool anySelected = Settings.Any(s => s.IsSelected);

            IsSelected = allSelected;
        }

        /// <summary>
        /// Converts a RegistryHive enum to its string representation (HKCU, HKLM, etc.)
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <returns>The string representation of the registry hive.</returns>
        private string GetRegistryHiveString(RegistryHive hive)
        {
            return hive switch
            {
                RegistryHive.ClassesRoot => "HKCR",
                RegistryHive.CurrentUser => "HKCU",
                RegistryHive.LocalMachine => "HKLM",
                RegistryHive.Users => "HKU",
                RegistryHive.CurrentConfig => "HKCC",
                _ => hive.ToString()
            };
        }
    }
}
