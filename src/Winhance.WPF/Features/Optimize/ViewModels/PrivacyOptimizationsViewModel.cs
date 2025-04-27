using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Interfaces;
using System.Collections.Generic;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for privacy optimizations.
    /// </summary>
    public partial class PrivacyOptimizationsViewModel : BaseSettingsViewModel<OptimizationSettingViewModel>
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
                        // Use the FromSetting method to create the view model with the new services
                        var viewModel = OptimizationSettingViewModel.FromSetting(
                            setting, _registryService, null, _logService, _dependencyManager, _viewModelLocator, _settingsRegistry);

                        // Log the dependencies for this setting
                        if (setting.Dependencies != null && setting.Dependencies.Any())
                        {
                            _logService.Log(LogLevel.Info, $"Setting {setting.Name} (ID: {setting.Id}) has {setting.Dependencies.Count} dependencies");
                            foreach (var dependency in setting.Dependencies)
                            {
                                _logService.Log(LogLevel.Info, $"Dependency: {dependency.DependentSettingId} depends on {dependency.RequiredSettingId} with type {dependency.DependencyType}");
                            }
                        }

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
                                string hiveString = GetRegistryHiveString(regSetting.Hive);
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

                                // Populate the LinkedRegistrySettingsWithValues collection for tooltip display
                                setting.LinkedRegistrySettingsWithValues.Clear();
                                foreach (var regSetting in setting.LinkedRegistrySettings.Settings)
                                {
                                    var regCurrentValue = await _registryService.GetCurrentValueAsync(regSetting);
                                    _logService.Log(LogLevel.Info, $"Current value for linked setting {regSetting.Name}: {regCurrentValue ?? "null"}");
                                    setting.LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(regSetting, regCurrentValue));
                                }
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
                            string hiveString = GetRegistryHiveString(setting.RegistrySetting.Hive);
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

        /// <summary>
        /// Applies all selected privacy settings.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task ApplySettingsAsync(IProgress<TaskProgressDetail> progress)
        {
            try
            {
                IsLoading = true;
                progress.Report(new TaskProgressDetail { StatusText = "Applying privacy settings...", IsIndeterminate = false, Progress = 0 });

                // Clear registry caches before applying settings
                _registryService.ClearRegistryCaches();
                _logService.Log(LogLevel.Info, "Cleared registry caches before applying settings");

                // Get all settings that need to be processed
                var selectedSettings = Settings.Where(s => s.IsSelected).ToList();

                // Also get settings that were previously selected but are now deselected
                var deselectedSettings = Settings.Where(s => !s.IsSelected).ToList();

                if (selectedSettings.Count == 0 && deselectedSettings.Count == 0)
                {
                    progress.Report(new TaskProgressDetail { StatusText = "No privacy settings selected", IsIndeterminate = false, Progress = 1.0 });
                    return;
                }

                // Special case: Check if "Send Diagnostic Data" is being disabled
                var sendDiagnosticDataSetting = deselectedSettings.FirstOrDefault(s =>
                    s.Name == "Send Diagnostic Data" ||
                    s.Id == "privacy-diagnostics-policy" ||
                    s.Id == "privacy-diagnostics-toast" ||
                    s.Id == "privacy-diagnostics-allow" ||
                    s.Id == "privacy-diagnostics-max");

                if (sendDiagnosticDataSetting != null)
                {
                    _logService.Log(LogLevel.Info, $"'Send Diagnostic Data' (ID: {sendDiagnosticDataSetting.Id}) is being disabled, checking if 'Improve Inking and Typing' needs to be disabled too");

                    // Find the Improve Inking and Typing settings
                    var inkingTypingSettings = selectedSettings.Where(s => s.Name == "Improve Inking and Typing").ToList();
                    if (inkingTypingSettings.Any())
                    {
                        _logService.Log(LogLevel.Info, $"Found {inkingTypingSettings.Count} 'Improve Inking and Typing' settings that need to be disabled");

                        foreach (var inkingSetting in inkingTypingSettings)
                        {
                            _logService.Log(LogLevel.Info, $"'Improve Inking and Typing' (ID: {inkingSetting.Id}) is selected but 'Send Diagnostic Data' is being disabled, removing it from selected settings");

                            // Remove it from the selected settings
                            selectedSettings.Remove(inkingSetting);

                            // Add it to the deselected settings if it's not already there
                            if (!deselectedSettings.Contains(inkingSetting))
                            {
                                deselectedSettings.Add(inkingSetting);
                            }

                            // Update the UI state
                            inkingSetting.IsUpdatingFromCode = true;
                            try
                            {
                                inkingSetting.IsSelected = false;
                            }
                            finally
                            {
                                inkingSetting.IsUpdatingFromCode = false;
                            }
                        }
                    }
                }

                // Check for settings with dependencies
                foreach (var deselectedSetting in deselectedSettings)
                {
                    // Find any selected settings that depend on this deselected setting
                    var dependentSettings = selectedSettings
                        .Where(s => s.Dependencies != null &&
                                s.Dependencies.Any(d => d.RequiredSettingId == deselectedSetting.Id &&
                                                    d.DependencyType == Winhance.Core.Features.Common.Models.SettingDependencyType.RequiresEnabled))
                        .ToList();

                    if (dependentSettings.Any())
                    {
                        _logService.Log(LogLevel.Info, $"'{deselectedSetting.Name}' is being disabled, checking for dependent settings");

                        foreach (var dependentSetting in dependentSettings)
                        {
                            _logService.Log(LogLevel.Info, $"'{dependentSetting.Name}' depends on '{deselectedSetting.Name}', removing it from selected settings");

                            // Remove it from the selected settings
                            selectedSettings.Remove(dependentSetting);

                            // Add it to the deselected settings if it's not already there
                            if (!deselectedSettings.Contains(dependentSetting))
                            {
                                deselectedSettings.Add(dependentSetting);
                            }

                            // Update the UI state
                            dependentSetting.IsUpdatingFromCode = true;
                            try
                            {
                                dependentSetting.IsSelected = false;
                            }
                            finally
                            {
                                dependentSetting.IsUpdatingFromCode = false;
                            }
                        }
                    }
                }

                int settingsProcessed = 0;
                int totalSettings = selectedSettings.Count;

                foreach (var setting in selectedSettings)
                {
                    // Handle linked registry settings
                    if (setting.LinkedRegistrySettings != null && setting.LinkedRegistrySettings.Settings.Count > 0)
                    {
                        _logService.Log(LogLevel.Info, $"Applying linked setting: {setting.Name} with {setting.LinkedRegistrySettings.Settings.Count} registry entries");

                        // Apply all linked settings
                        bool result = await _registryService.ApplyLinkedSettingsAsync(setting.LinkedRegistrySettings, true);

                        // Check if this setting has dependencies that require other settings to be enabled
                        if (result && setting.Dependencies != null && setting.Dependencies.Any(d => d.DependencyType == SettingDependencyType.RequiresEnabled))
                        {
                            foreach (var dependency in setting.Dependencies.Where(d => d.DependencyType == SettingDependencyType.RequiresEnabled))
                            {
                                // Find the required setting
                                var requiredSetting = Settings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                                if (requiredSetting != null)
                                {
                                    _logService.Log(LogLevel.Info, $"Checking if '{requiredSetting.Name}' needs to be enabled as required by '{setting.Name}'");

                                    // Only apply if it's not already selected
                                    if (!requiredSetting.IsSelected)
                                    {
                                        _logService.Log(LogLevel.Info, $"'{requiredSetting.Name}' was not enabled, enabling it now");

                                        // Set the selection state
                                        requiredSetting.IsUpdatingFromCode = true;
                                        try
                                        {
                                            requiredSetting.IsSelected = true;
                                        }
                                        finally
                                        {
                                            requiredSetting.IsUpdatingFromCode = false;
                                        }

                                        // Apply the linked settings
                                        if (requiredSetting.LinkedRegistrySettings != null &&
                                            requiredSetting.LinkedRegistrySettings.Settings.Count > 0)
                                        {
                                            bool requiredResult = await _registryService.ApplyLinkedSettingsAsync(
                                                requiredSetting.LinkedRegistrySettings, true);

                                            _logService.Log(requiredResult ?
                                                LogLevel.Success : LogLevel.Error,
                                                $"Automatically enabled '{requiredSetting.Name}': {(requiredResult ? "Success" : "Failed")}");
                                        }
                                        else if (requiredSetting.RegistrySetting != null)
                                        {
                                            // Apply single registry setting
                                            string hiveString = GetRegistryHiveString(requiredSetting.RegistrySetting.Hive);
                                            string fullPath = $"{hiveString}\\{requiredSetting.RegistrySetting.SubKey}";
                                            object valueToSet = requiredSetting.RegistrySetting.EnabledValue ?? requiredSetting.RegistrySetting.RecommendedValue;
                                            bool requiredResult = _registryService.SetValue(fullPath, requiredSetting.RegistrySetting.Name, valueToSet, requiredSetting.RegistrySetting.ValueType);

                                            _logService.Log(requiredResult ?
                                                LogLevel.Success : LogLevel.Error,
                                                $"Automatically enabled '{requiredSetting.Name}': {(requiredResult ? "Success" : "Failed")}");
                                        }
                                    }
                                    else
                                    {
                                        _logService.Log(LogLevel.Info, $"'{requiredSetting.Name}' was already enabled");
                                    }
                                }
                                else
                                {
                                    _logService.Log(LogLevel.Warning, $"Could not find required setting '{dependency.RequiredSettingId}' to enable automatically");
                                }
                            }
                        }

                        settingsProcessed++;
                        progress.Report(new TaskProgressDetail
                        {
                            StatusText = result ? $"Applied setting: {setting.Name}" : $"Failed to apply setting: {setting.Name}",
                            IsIndeterminate = false,
                            Progress = (double)settingsProcessed / totalSettings
                        });
                    }
                    // Handle single registry setting
                    else if (setting.RegistrySetting != null)
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
                        bool result = _registryService.SetValue(fullPath, setting.RegistrySetting.Name, valueToSet, setting.RegistrySetting.ValueType);

                        settingsProcessed++;
                        progress.Report(new TaskProgressDetail
                        {
                            StatusText = result ? $"Applied setting: {setting.Name}" : $"Failed to apply setting: {setting.Name}",
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

                progress.Report(new TaskProgressDetail { StatusText = "Privacy settings applied successfully", IsIndeterminate = false, Progress = 1.0 });
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying privacy settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Converts a RegistryHive enum to its string representation (HKCU, HKLM, etc.)
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <returns>The string representation of the registry hive.</returns>
        private string GetRegistryHiveString(Microsoft.Win32.RegistryHive hive)
        {
            return hive switch
            {
                Microsoft.Win32.RegistryHive.ClassesRoot => "HKCR",
                Microsoft.Win32.RegistryHive.CurrentUser => "HKCU",
                Microsoft.Win32.RegistryHive.LocalMachine => "HKLM",
                Microsoft.Win32.RegistryHive.Users => "HKU",
                Microsoft.Win32.RegistryHive.CurrentConfig => "HKCC",
                _ => throw new ArgumentException($"Unsupported registry hive: {hive}")
            };
        }

        /// <summary>
        /// Restores all selected privacy settings to their default values.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task RestoreDefaultsAsync(IProgress<TaskProgressDetail> progress)
        {
            try
            {
                IsLoading = true;
                progress.Report(new TaskProgressDetail { StatusText = "Restoring privacy settings to defaults...", IsIndeterminate = false, Progress = 0 });

                // Clear registry caches before restoring settings
                _registryService.ClearRegistryCaches();
                _logService.Log(LogLevel.Info, "Cleared registry caches before restoring settings");

                var selectedSettings = Settings.Where(s => s.IsSelected).ToList();
                if (selectedSettings.Count == 0)
                {
                    progress.Report(new TaskProgressDetail { StatusText = "No privacy settings selected", IsIndeterminate = false, Progress = 1.0 });
                    return;
                }

                // Special case: Check if "Send Diagnostic Data" and "Improve Inking and Typing" are both selected
                var sendDiagnosticDataSetting = selectedSettings.FirstOrDefault(s =>
                    s.Name == "Send Diagnostic Data" ||
                    s.Id == "privacy-diagnostics-policy" ||
                    s.Id == "privacy-diagnostics-toast" ||
                    s.Id == "privacy-diagnostics-allow" ||
                    s.Id == "privacy-diagnostics-max");

                var inkingTypingSettings = selectedSettings.Where(s => s.Name == "Improve Inking and Typing").ToList();

                if (sendDiagnosticDataSetting != null && inkingTypingSettings.Any())
                {
                    _logService.Log(LogLevel.Info, $"Both 'Send Diagnostic Data' (ID: {sendDiagnosticDataSetting.Id}) and 'Improve Inking and Typing' are selected for restore to defaults");
                    _logService.Log(LogLevel.Info, "Ensuring 'Improve Inking and Typing' is disabled if 'Send Diagnostic Data' is disabled");

                    // If Send Diagnostic Data is being set to disabled, also disable Improve Inking and Typing
                    if (sendDiagnosticDataSetting.RegistrySetting != null &&
                        !sendDiagnosticDataSetting.RegistrySetting.RecommendedValue.Equals(sendDiagnosticDataSetting.RegistrySetting.EnabledValue))
                    {
                        _logService.Log(LogLevel.Info, "'Send Diagnostic Data' will be disabled, also disabling 'Improve Inking and Typing'");

                        foreach (var inkingSetting in inkingTypingSettings)
                        {
                            _logService.Log(LogLevel.Info, $"Automatically disabling 'Improve Inking and Typing' (ID: {inkingSetting.Id})");

                            // Ensure Improve Inking and Typing is also disabled
                            inkingSetting.IsUpdatingFromCode = true;
                            try
                            {
                                inkingSetting.IsSelected = false;
                            }
                            finally
                            {
                                inkingSetting.IsUpdatingFromCode = false;
                            }
                        }
                    }
                }

                // Check for settings with dependencies
                foreach (var setting in selectedSettings.ToList())
                {
                    if (setting.Dependencies != null && setting.Dependencies.Any())
                    {
                        _logService.Log(LogLevel.Info, $"'{setting.Name}' has dependencies, checking them");

                        foreach (var dependency in setting.Dependencies)
                        {
                            var requiredSetting = selectedSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                            if (requiredSetting != null)
                            {
                                _logService.Log(LogLevel.Info, $"'{setting.Name}' depends on '{requiredSetting.Name}', both are selected for restore to defaults");
                                _logService.Log(LogLevel.Info, $"Ensuring '{setting.Name}' is disabled if '{requiredSetting.Name}' is disabled");
                            }
                        }
                    }
                }

                int settingsProcessed = 0;
                int totalSettings = selectedSettings.Count;

                foreach (var setting in selectedSettings)
                {
                    // Handle linked registry settings
                    if (setting.LinkedRegistrySettings != null && setting.LinkedRegistrySettings.Settings.Count > 0)
                    {
                        _logService.Log(LogLevel.Info, $"Restoring linked setting: {setting.Name} with {setting.LinkedRegistrySettings.Settings.Count} registry entries");

                        // Restore all linked settings (apply with enable=false)
                        bool result = await _registryService.ApplyLinkedSettingsAsync(setting.LinkedRegistrySettings, false);

                        settingsProcessed++;
                        progress.Report(new TaskProgressDetail
                        {
                            StatusText = result ? $"Restored setting: {setting.Name}" : $"Failed to restore setting: {setting.Name}",
                            IsIndeterminate = false,
                            Progress = (double)settingsProcessed / totalSettings
                        });
                    }
                    // Handle single registry setting
                    else if (setting.RegistrySetting != null)
                    {
                        // Use DisabledValue if available, otherwise fall back to DefaultValue for backward compatibility
                        object? valueToSet = setting.RegistrySetting.DisabledValue ?? setting.RegistrySetting.DefaultValue;

                        bool result = false;
                        if (valueToSet == null)
                        {
                            result = await _registryService.DeleteValue(setting.RegistrySetting.Hive, setting.RegistrySetting.SubKey, setting.RegistrySetting.Name);
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
                            result = _registryService.SetValue(fullPath, setting.RegistrySetting.Name, valueToSet, setting.RegistrySetting.ValueType);
                        }

                        settingsProcessed++;
                        progress.Report(new TaskProgressDetail
                        {
                            StatusText = result ? $"Restored setting: {setting.Name}" : $"Failed to restore setting: {setting.Name}",
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

                progress.Report(new TaskProgressDetail { StatusText = "Privacy settings restored to defaults successfully", IsIndeterminate = false, Progress = 1.0 });
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error restoring privacy settings to defaults: {ex.Message}");
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
