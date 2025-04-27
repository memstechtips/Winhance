using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for gaming and performance optimizations.
    /// </summary>
    public partial class GamingandPerformanceOptimizationsViewModel : BaseSettingsViewModel<OptimizationSettingViewModel>
    {
        private readonly IViewModelLocator? _viewModelLocator;
        private readonly ISettingsRegistry? _settingsRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="GamingandPerformanceOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="viewModelLocator">The view model locator.</param>
        /// <param name="settingsRegistry">The settings registry.</param>
        public GamingandPerformanceOptimizationsViewModel(
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
        /// Loads the gaming settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, "Loading gaming and performance settings");

                // Initialize gaming and performance settings from GamingandPerformanceOptimizations.cs
                var gamingOptimizations = Core.Features.Optimize.Models.GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
                if (gamingOptimizations?.Settings != null)
                {
                    Settings.Clear();

                    // Add all settings sorted alphabetically by name
                    foreach (var setting in gamingOptimizations.Settings.OrderBy(s => s.Name))
                    {
                        var viewModel = new OptimizationSettingViewModel(
                            _registryService, 
                            null, 
                            _logService, 
                            null, 
                            _viewModelLocator, 
                            _settingsRegistry)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            IsSelected = false, // Always initialize as unchecked
                            GroupName = setting.GroupName,
                            RegistrySetting = setting.RegistrySettings.FirstOrDefault(),
                            ControlType = ControlType.BinaryToggle // Gaming settings are typically binary toggles
                        };

                        Settings.Add(viewModel);
                    }

                    // Set up property change handlers for checkboxes
                    foreach (var setting in Settings)
                    {
                        setting.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(OptimizationSettingViewModel.IsSelected))
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
                _logService.Log(LogLevel.Error, $"Error loading gaming and performance settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the status of all gaming and performance settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, $"Checking status for {Settings.Count} gaming and performance settings");

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
                            setting.LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(setting.RegistrySetting, currentValue));

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
                _logService.Log(LogLevel.Error, $"Error checking gaming and performance setting statuses: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Applies all selected gaming and performance settings.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task ApplySettingsAsync(IProgress<TaskProgressDetail> progress)
        {
            try
            {
                IsLoading = true;
                progress.Report(new TaskProgressDetail { StatusText = "Applying gaming and performance settings...", IsIndeterminate = false, Progress = 0 });

                var selectedSettings = Settings.Where(s => s.IsSelected).ToList();
                if (selectedSettings.Count == 0)
                {
                    progress.Report(new TaskProgressDetail { StatusText = "No gaming and performance settings selected", IsIndeterminate = false, Progress = 1.0 });
                    return;
                }

                int settingsProcessed = 0;
                int totalSettings = selectedSettings.Count;

                foreach (var setting in selectedSettings)
                {
                    if (setting.RegistrySetting != null)
                    {
                        string hiveString = setting.RegistrySetting.Hive.ToString();
                        if (hiveString == "LocalMachine") hiveString = "HKLM";
                        else if (hiveString == "CurrentUser") hiveString = "HKCU";
                        else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                        else if (hiveString == "Users") hiveString = "HKU";
                        else if (hiveString == "CurrentConfig") hiveString = "HKCC";

                        string fullPath = $"{hiveString}\\{setting.RegistrySetting.SubKey}";
                        // Use EnabledValue if available, otherwise fall back to RecommendedValue for backward compatibility
                        object valueToSet = setting.RegistrySetting.EnabledValue ?? setting.RegistrySetting.RecommendedValue;
                        _registryService.SetValue(fullPath, setting.RegistrySetting.Name, valueToSet, setting.RegistrySetting.ValueType);

                        settingsProcessed++;
                        progress.Report(new TaskProgressDetail
                        {
                            StatusText = $"Applied setting: {setting.Name}",
                            IsIndeterminate = false,
                            Progress = (double)settingsProcessed / totalSettings
                        });
                    }

                    // If this is a grouped setting, apply all child settings too
                    if (setting.IsGroupedSetting && setting.ChildSettings.Count > 0)
                    {
                        foreach (var childSetting in setting.ChildSettings.Where(c => c.IsSelected))
                        {
                            if (childSetting.RegistrySetting != null)
                            {
                                string hiveString = childSetting.RegistrySetting.Hive.ToString();
                                if (hiveString == "LocalMachine") hiveString = "HKLM";
                                else if (hiveString == "CurrentUser") hiveString = "HKCU";
                                else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                                else if (hiveString == "Users") hiveString = "HKU";
                                else if (hiveString == "CurrentConfig") hiveString = "HKCC";

                                string fullPath = $"{hiveString}\\{childSetting.RegistrySetting.SubKey}";
                                // Use EnabledValue if available, otherwise fall back to RecommendedValue for backward compatibility
                                object valueToSet = childSetting.RegistrySetting.EnabledValue ?? childSetting.RegistrySetting.RecommendedValue;
                                _registryService.SetValue(fullPath, childSetting.RegistrySetting.Name, valueToSet, childSetting.RegistrySetting.ValueType);

                                settingsProcessed++;
                                progress.Report(new TaskProgressDetail
                                {
                                    StatusText = $"Applied setting: {childSetting.Name}",
                                    IsIndeterminate = false,
                                    Progress = (double)settingsProcessed / totalSettings
                                });
                            }
                        }
                    }
                }

                // Refresh registry setting statuses to update the status indicators
                progress.Report(new TaskProgressDetail { StatusText = "Refreshing setting statuses...", IsIndeterminate = false, Progress = 0.95 });
                await CheckSettingStatusesAsync();

                progress.Report(new TaskProgressDetail { StatusText = "Gaming and performance settings applied successfully", IsIndeterminate = false, Progress = 1.0 });
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying gaming and performance settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Restores all selected gaming and performance settings to their default values.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task RestoreDefaultsAsync(IProgress<TaskProgressDetail> progress)
        {
            try
            {
                IsLoading = true;
                progress.Report(new TaskProgressDetail { StatusText = "Restoring gaming and performance settings to defaults...", IsIndeterminate = false, Progress = 0 });

                var selectedSettings = Settings.Where(s => s.IsSelected).ToList();
                if (selectedSettings.Count == 0)
                {
                    progress.Report(new TaskProgressDetail { StatusText = "No gaming and performance settings selected", IsIndeterminate = false, Progress = 1.0 });
                    return;
                }

                int settingsProcessed = 0;
                int totalSettings = selectedSettings.Count;

                foreach (var setting in selectedSettings)
                {
                    if (setting.RegistrySetting != null)
                    {
                        // Use DisabledValue if available, otherwise fall back to DefaultValue for backward compatibility
                        object? valueToSet = setting.RegistrySetting.DisabledValue ?? setting.RegistrySetting.DefaultValue;

                        if (valueToSet == null)
                        {
                            await _registryService.DeleteValue(setting.RegistrySetting.Hive, setting.RegistrySetting.SubKey, setting.RegistrySetting.Name);
                        }
                        else
                        {
                            string hiveString = setting.RegistrySetting.Hive.ToString();
                            if (hiveString == "LocalMachine") hiveString = "HKLM";
                            else if (hiveString == "CurrentUser") hiveString = "HKCU";
                            else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                            else if (hiveString == "Users") hiveString = "HKU";
                            else if (hiveString == "CurrentConfig") hiveString = "HKCC";

                            string fullPath = $"{hiveString}\\{setting.RegistrySetting.SubKey}";
                            _registryService.SetValue(fullPath, setting.RegistrySetting.Name, valueToSet, setting.RegistrySetting.ValueType);
                        }

                        settingsProcessed++;
                        progress.Report(new TaskProgressDetail
                        {
                            StatusText = $"Restored setting: {setting.Name}",
                            IsIndeterminate = false,
                            Progress = (double)settingsProcessed / totalSettings
                        });
                    }

                    // If this is a grouped setting, restore all child settings too
                    if (setting.IsGroupedSetting && setting.ChildSettings.Count > 0)
                    {
                        foreach (var childSetting in setting.ChildSettings.Where(c => c.IsSelected))
                        {
                            if (childSetting.RegistrySetting != null)
                            {
                                // Use DisabledValue if available, otherwise fall back to DefaultValue for backward compatibility
                                object? valueToSet = childSetting.RegistrySetting.DisabledValue ?? childSetting.RegistrySetting.DefaultValue;

                                if (valueToSet == null)
                                {
                                    await _registryService.DeleteValue(childSetting.RegistrySetting.Hive, childSetting.RegistrySetting.SubKey, childSetting.RegistrySetting.Name);
                                }
                                else
                                {
                                    string hiveString = childSetting.RegistrySetting.Hive.ToString();
                                    if (hiveString == "LocalMachine") hiveString = "HKLM";
                                    else if (hiveString == "CurrentUser") hiveString = "HKCU";
                                    else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                                    else if (hiveString == "Users") hiveString = "HKU";
                                    else if (hiveString == "CurrentConfig") hiveString = "HKCC";

                                    string fullPath = $"{hiveString}\\{childSetting.RegistrySetting.SubKey}";
                                    _registryService.SetValue(fullPath, childSetting.RegistrySetting.Name, valueToSet, childSetting.RegistrySetting.ValueType);
                                }

                                settingsProcessed++;
                                progress.Report(new TaskProgressDetail
                                {
                                    StatusText = $"Restored setting: {childSetting.Name}",
                                    IsIndeterminate = false,
                                    Progress = (double)settingsProcessed / totalSettings
                                });
                            }
                        }
                    }

                    // Uncheck the setting after restoring
                    setting.IsSelected = false;
                }

                // Refresh registry setting statuses to update the status indicators
                progress.Report(new TaskProgressDetail { StatusText = "Refreshing setting statuses...", IsIndeterminate = false, Progress = 0.95 });
                await CheckSettingStatusesAsync();

                progress.Report(new TaskProgressDetail { StatusText = "Gaming and performance settings restored to defaults successfully", IsIndeterminate = false, Progress = 1.0 });
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error restoring gaming and performance settings to defaults: {ex.Message}");
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
        private string GetStatusMessage(ApplicationSettingViewModel setting)
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
