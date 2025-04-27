using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Customize.Models;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Sound optimizations.
    /// </summary>
    public partial class SoundOptimizationsViewModel : BaseSettingsViewModel<CustomizationSettingItem>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SoundOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        public SoundOptimizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService)
            : base(progressService, registryService, logService)
        {
        }

        /// <summary>
        /// Loads the Sound settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                // Clear existing settings
                Settings.Clear();

                // Load Sound optimizations from SoundOptimizations
                var soundOptimizations = Core.Features.Optimize.Models.SoundOptimizations.GetSoundOptimizations();
                if (soundOptimizations?.Settings != null)
                {
                    // Add settings sorted alphabetically by name
                    foreach (var setting in soundOptimizations.Settings.OrderBy(s => s.Name))
                    {
                        var customizationSetting = new CustomizationSettingItem(_registryService, null, _logService);

                        // Use reflection to set properties to avoid ambiguity
                        typeof(CustomizationSettingItem).GetProperty("Id")?.SetValue(customizationSetting, setting.Id);
                        typeof(CustomizationSettingItem).GetProperty("Name")?.SetValue(customizationSetting, setting.Name);
                        
                        
                        typeof(CustomizationSettingItem).GetProperty("Description")?.SetValue(customizationSetting, setting.Description);
                        typeof(CustomizationSettingItem).GetProperty("GroupName")?.SetValue(customizationSetting, setting.GroupName);
                        typeof(CustomizationSettingItem).GetProperty("IsSelected")?.SetValue(customizationSetting, setting.IsEnabled);
                        typeof(CustomizationSettingItem).GetProperty("RegistrySetting")?.SetValue(customizationSetting, setting.RegistrySettings.FirstOrDefault());
                        typeof(CustomizationSettingItem).GetProperty("IsGroupHeader")?.SetValue(customizationSetting, false);
                        typeof(CustomizationSettingItem).GetProperty("ControlType")?.SetValue(customizationSetting, ControlType.BinaryToggle);

                        Settings.Add(customizationSetting);
                    }
                }

                // Check setting statuses
                await CheckSettingStatusesAsync();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Sound settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the status of all Sound settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                foreach (var setting in Settings)
                {
                    // Use reflection to get and set properties to avoid ambiguity
                    PropertyInfo? registrySettingProp = typeof(CustomizationSettingItem).GetProperty("RegistrySetting");
                    var registrySetting = registrySettingProp?.GetValue(setting) as RegistrySetting;

                    if (registrySetting != null)
                    {
                        // Get status
                        var status = await _registryService.GetSettingStatusAsync(registrySetting);
                        typeof(CustomizationSettingItem).GetProperty("Status")?.SetValue(setting, status);

                        // Get current value
                        var currentValue = await _registryService.GetCurrentValueAsync(registrySetting);
                        typeof(CustomizationSettingItem).GetProperty("CurrentValue")?.SetValue(setting, currentValue);

                        // Set status message
                        string statusMessage = GetStatusMessage(setting);
                        typeof(CustomizationSettingItem).GetProperty("StatusMessage")?.SetValue(setting, statusMessage);

                        // Set the IsUpdatingFromCode flag to prevent automatic application
                        typeof(CustomizationSettingItem).GetProperty("IsUpdatingFromCode")?.SetValue(setting, true);

                        try
                        {
                            // Update IsSelected based on status
                            bool shouldBeSelected = status == RegistrySettingStatus.Applied;

                            // Set the checkbox state to match the registry state
                            _logService.Log(LogLevel.Info, $"Setting {setting.Name} status is {status}, setting IsSelected to {shouldBeSelected}");
                            typeof(CustomizationSettingItem).GetProperty("IsSelected")?.SetValue(setting, shouldBeSelected);
                        }
                        finally
                        {
                            // Reset the flag
                            typeof(CustomizationSettingItem).GetProperty("IsUpdatingFromCode")?.SetValue(setting, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking Sound setting statuses: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the status message for a setting.
        /// </summary>
        /// <param name="setting">The setting.</param>
        /// <returns>The status message.</returns>
        private string GetStatusMessage(CustomizationSettingItem setting)
        {
            // Use reflection to get properties to avoid ambiguity
            var status = (RegistrySettingStatus)typeof(CustomizationSettingItem).GetProperty("Status")?.GetValue(setting);
            string message = status switch
            {
                RegistrySettingStatus.Applied => "Setting is applied with recommended value",
                RegistrySettingStatus.NotApplied => "Setting is not applied or using default value",
                RegistrySettingStatus.Modified => "Setting has a custom value different from recommended",
                RegistrySettingStatus.Error => "Error checking setting status",
                _ => "Unknown status"
            };

            // Add current value if available
            var currentValue = typeof(CustomizationSettingItem).GetProperty("CurrentValue")?.GetValue(setting);
            if (currentValue != null)
            {
                message += $"\nCurrent value: {currentValue}";
            }

            // Add recommended value if available
            var registrySetting = typeof(CustomizationSettingItem).GetProperty("RegistrySetting")?.GetValue(setting) as RegistrySetting;
            if (registrySetting?.RecommendedValue != null)
            {
                message += $"\nRecommended value: {registrySetting.RecommendedValue}";
            }

            // Add default value if available
            if (registrySetting?.DefaultValue != null)
            {
                message += $"\nDefault value: {registrySetting.DefaultValue}";
            }

            return message;
        }

        /// <summary>
        /// Applies all selected Sound settings.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task ApplySettingsAsync(IProgress<TaskProgressDetail> progress)
        {
            try
            {
                // Use reflection to filter settings
                var selectedSettings = Settings.Where(s =>
                {
                    bool isSelected = (bool)typeof(CustomizationSettingItem).GetProperty("IsSelected")?.GetValue(s);
                    var registrySetting = typeof(CustomizationSettingItem).GetProperty("RegistrySetting")?.GetValue(s) as RegistrySetting;
                    return isSelected && registrySetting != null;
                }).ToList();

                if (selectedSettings.Count == 0)
                {
                    return;
                }

                int current = 0;
                int total = selectedSettings.Count;

                foreach (var setting in selectedSettings)
                {
                    var registrySetting = typeof(CustomizationSettingItem).GetProperty("RegistrySetting")?.GetValue(setting) as RegistrySetting;
                    var name = typeof(CustomizationSettingItem).GetProperty("Name")?.GetValue(setting) as string;

                    if (registrySetting != null && name != null)
                    {
                        current++;
                        progress?.Report(new TaskProgressDetail
                        {
                            StatusText = $"Applying {name}",
                            Progress = (int)((double)current / total * 100)
                        });

                        string hiveString = registrySetting.Hive.ToString();
                        if (hiveString == "LocalMachine") hiveString = "HKLM";
                        else if (hiveString == "CurrentUser") hiveString = "HKCU";
                        else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                        else if (hiveString == "Users") hiveString = "HKU";
                        else if (hiveString == "CurrentConfig") hiveString = "HKCC";

                        string fullPath = $"{hiveString}\\{registrySetting.SubKey}";
                        _registryService.SetValue(
                            fullPath,
                            registrySetting.Name,
                            registrySetting.RecommendedValue,
                            registrySetting.ValueType);
                    }
                }

                // Refresh setting statuses
                await CheckSettingStatusesAsync();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Sound settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Restores all selected Sound settings to their default values.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task RestoreDefaultsAsync(IProgress<TaskProgressDetail> progress)
        {
            try
            {
                // Use reflection to filter settings
                var selectedSettings = Settings.Where(s =>
                {
                    bool isSelected = (bool)typeof(CustomizationSettingItem).GetProperty("IsSelected")?.GetValue(s);
                    var registrySetting = typeof(CustomizationSettingItem).GetProperty("RegistrySetting")?.GetValue(s) as RegistrySetting;
                    return isSelected && registrySetting != null;
                }).ToList();

                if (selectedSettings.Count == 0)
                {
                    return;
                }

                int current = 0;
                int total = selectedSettings.Count;

                foreach (var setting in selectedSettings)
                {
                    var registrySetting = typeof(CustomizationSettingItem).GetProperty("RegistrySetting")?.GetValue(setting) as RegistrySetting;
                    var name = typeof(CustomizationSettingItem).GetProperty("Name")?.GetValue(setting) as string;

                    if (registrySetting != null && name != null)
                    {
                        current++;
                        progress?.Report(new TaskProgressDetail
                        {
                            StatusText = $"Restoring {name} to default",
                            Progress = (int)((double)current / total * 100)
                        });

                        if (registrySetting.DefaultValue == null)
                        {
                            await _registryService.DeleteValue(
                                registrySetting.Hive,
                                registrySetting.SubKey,
                                registrySetting.Name);
                        }
                        else
                        {
                            string hiveString = registrySetting.Hive.ToString();
                            if (hiveString == "LocalMachine") hiveString = "HKLM";
                            else if (hiveString == "CurrentUser") hiveString = "HKCU";
                            else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                            else if (hiveString == "Users") hiveString = "HKU";
                            else if (hiveString == "CurrentConfig") hiveString = "HKCC";

                            string fullPath = $"{hiveString}\\{registrySetting.SubKey}";
                            _registryService.SetValue(
                                fullPath,
                                registrySetting.Name,
                                registrySetting.DefaultValue,
                                registrySetting.ValueType);
                        }
                    }
                }

                // Refresh setting statuses
                await CheckSettingStatusesAsync();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error restoring Sound settings: {ex.Message}");
                throw;
            }
        }
    }
}
