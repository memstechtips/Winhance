using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Extensions;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Start Menu customizations.
    /// </summary>
    public partial class StartMenuCustomizationsViewModel : BaseSettingsViewModel<ApplicationSettingItem>
    {
        private readonly ISystemServices _systemServices;
        private bool _isWindows11;

        /// <summary>
        /// Gets the command to clean the Start Menu.
        /// </summary>
        [RelayCommand]
        public async Task CleanStartMenu()
        {
            try
            {
                // Start task with progress
                _progressService.StartTask("Cleaning Start Menu...");
                
                // Update initial progress
                _progressService.UpdateDetailedProgress(new TaskProgressDetail
                {
                    StatusText = "Cleaning Start Menu...",
                    Progress = 0
                });

                // Determine Windows version
                _isWindows11 = _systemServices.IsWindows11();

                // Clean Start Menu
                await Task.Run(() =>
                {
                    StartMenuCustomizations.CleanStartMenu(_isWindows11, _systemServices);
                });

                // Log success
                _logService.Log(LogLevel.Info, "Start Menu cleaned successfully");

                // Update completion progress
                _progressService.UpdateDetailedProgress(new TaskProgressDetail
                {
                    StatusText = "Start Menu cleaned successfully",
                    Progress = 100
                });
                
                // Complete the task
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                // Log error
                _logService.Log(LogLevel.Error, $"Error cleaning Start Menu: {ex.Message}");

                // Update error progress
                _progressService.UpdateDetailedProgress(new TaskProgressDetail
                {
                    StatusText = $"Error cleaning Start Menu: {ex.Message}",
                    Progress = 100
                });
                
                // Complete the task
                _progressService.CompleteTask();
            }
        }

        /// <summary>
        /// Gets the command to execute an action.
        /// </summary>
        [RelayCommand]
        public async Task ExecuteAction(ApplicationAction? action)
        {
            if (action == null) return;

            try
            {
                // Execute the registry action if present
                if (action.RegistrySetting != null)
                {
                    string hiveString = action.RegistrySetting.Hive.ToString();
                    if (hiveString == "LocalMachine") hiveString = "HKLM";
                    else if (hiveString == "CurrentUser") hiveString = "HKCU";
                    else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                    else if (hiveString == "Users") hiveString = "HKU";
                    else if (hiveString == "CurrentConfig") hiveString = "HKCC";

                    string fullPath = $"{hiveString}\\{action.RegistrySetting.SubKey}";
                    _registryService.SetValue(
                        fullPath,
                        action.RegistrySetting.Name,
                        action.RegistrySetting.RecommendedValue,
                        action.RegistrySetting.ValueType);
                }

                // Execute custom action if present
                if (action.CustomAction != null)
                {
                    await action.CustomAction();
                }

                _logService.Log(LogLevel.Info, $"Action '{action.Name}' executed successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error executing action '{action.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the collection of Start Menu actions.
        /// </summary>
        public ObservableCollection<ApplicationAction> Actions { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="StartMenuCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="systemServices">The system services.</param>
        public StartMenuCustomizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            ISystemServices systemServices)
            : base(progressService, registryService, logService)
        {
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _isWindows11 = _systemServices.IsWindows11();
        }

        /// <summary>
        /// Loads the Start Menu customizations.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                // Clear existing settings
                Settings.Clear();

                // Load Start Menu customizations
                var startMenuCustomizations = Core.Features.Customize.Models.StartMenuCustomizations.GetStartMenuCustomizations();
                if (startMenuCustomizations?.Settings != null)
                {
                    // Add settings sorted alphabetically by name
                    foreach (var setting in startMenuCustomizations.Settings.OrderBy(s => s.Name))
                    {
                        // Skip Windows 11 specific settings on Windows 10
                        if (!_isWindows11 && setting.IsWindows11Only)
                        {
                            continue;
                        }

                        // Skip Windows 10 specific settings on Windows 11
                        if (_isWindows11 && setting.IsWindows10Only)
                        {
                            continue;
                        }

                        // Create ApplicationSettingItem directly
                        var settingItem = new ApplicationSettingItem(_registryService, null, _logService)
                        {
                            Id = setting.Id,
                            Name = setting.Name,
                            Description = setting.Description,
                            ControlType = setting.ControlType,
                            IsWindows11Only = setting.IsWindows11Only,
                            IsWindows10Only = setting.IsWindows10Only
                        };

                        // Add any actions
                        var actionsProperty = setting.GetType().GetProperty("Actions");
                        if (actionsProperty != null && 
                            actionsProperty.GetValue(setting) is IEnumerable<object> actions && 
                            actions.Any())
                        {
                            // We need to handle this differently since the Actions property doesn't exist in ApplicationSetting
                            // This is a temporary workaround until we refactor the code properly
                        }

                        // Set up the registry settings
                        if (setting.RegistrySettings != null && setting.RegistrySettings.Count == 1)
                        {
                            // Single registry setting
                            settingItem.RegistrySetting = setting.RegistrySettings[0];
                            _logService.Log(LogLevel.Info, $"Setting up single registry setting for {setting.Name}: {setting.RegistrySettings[0].Hive}\\{setting.RegistrySettings[0].SubKey}\\{setting.RegistrySettings[0].Name}");
                        }
                        else if (setting.RegistrySettings != null && setting.RegistrySettings.Count > 1)
                        {
                            // Linked registry settings
                            var linkedSettings = new LinkedRegistrySettings();
                            foreach (var regSetting in setting.RegistrySettings)
                            {
                                linkedSettings.Settings.Add(regSetting);
                                _logService.Log(LogLevel.Info, $"Adding linked registry setting for {setting.Name}: {regSetting.Hive}\\{regSetting.SubKey}\\{regSetting.Name}");
                            }
                            settingItem.LinkedRegistrySettings = linkedSettings;
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"No registry settings found for {setting.Name}");
                        }

                        Settings.Add(settingItem);
                    }

                    // Set up property change handlers for settings
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

                // Check setting statuses
                await CheckSettingStatusesAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Initializes the Start Menu actions.
        /// </summary>
        private void InitializeActions()
        {
            Actions.Clear();

            // Add Clean Start Menu action
            Actions.Add(new ApplicationAction
            {
                Id = "clean-start-menu",
                Name = "Clean Start Menu",
                Description = "Cleans the Start Menu by removing pinned apps and restoring the default layout",
                CustomAction = async () =>
                {
                    await CleanStartMenu();
                    return true;
                }
            });
        }

        /// <summary>
        /// Checks the status of all Start Menu settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task CheckSettingStatusesAsync()
        {
            try
            {
                foreach (var setting in Settings)
                {
                    // Get status
                    var status = await _registryService.GetSettingStatusAsync(setting.RegistrySetting);
                    setting.Status = status;

                    // Get current value
                    var currentValue = await _registryService.GetCurrentValueAsync(setting.RegistrySetting);
                    setting.CurrentValue = currentValue;

                    // Set IsRegistryValueNull property based on current value
                    setting.IsRegistryValueNull = currentValue == null;

                    // Update LinkedRegistrySettingsWithValues for tooltip display
                    var linkedRegistrySettingsWithValues = new ObservableCollection<LinkedRegistrySettingWithValue>();
                    
                    // Get the LinkedRegistrySettings property
                    var linkedRegistrySettings = setting.LinkedRegistrySettings;
                    
                    if (linkedRegistrySettings != null && linkedRegistrySettings.Settings.Count > 0)
                    {
                        // For linked settings, get fresh values from registry
                        bool anyNull = false;
                        foreach (var regSetting in linkedRegistrySettings.Settings)
                        {
                            string hiveString = regSetting.Hive.ToString();
                            if (hiveString == "LocalMachine") hiveString = "HKLM";
                            else if (hiveString == "CurrentUser") hiveString = "HKCU";
                            else if (hiveString == "ClassesRoot") hiveString = "HKCR";
                            else if (hiveString == "Users") hiveString = "HKU";
                            else if (hiveString == "CurrentConfig") hiveString = "HKCC";
                            
                            var regCurrentValue = _registryService.GetValue(
                                $"{hiveString}\\{regSetting.SubKey}",
                                regSetting.Name);
                                
                            if (regCurrentValue == null)
                            {
                                anyNull = true;
                            }
                                
                            linkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(regSetting, regCurrentValue));
                        }
                        
                        // For linked settings, set IsRegistryValueNull if any value is null
                        setting.IsRegistryValueNull = anyNull;
                    }
                    else if (setting.RegistrySetting != null)
                    {
                        // For single setting
                        linkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(setting.RegistrySetting, currentValue));
                        // IsRegistryValueNull is already set above
                    }
                    
                    setting.LinkedRegistrySettingsWithValues = linkedRegistrySettingsWithValues;

                    // Set status message
                    string statusMessage = GetStatusMessage(setting);
                    setting.StatusMessage = statusMessage;

                    // Set the IsUpdatingFromCode flag to prevent automatic application
                    setting.IsUpdatingFromCode = true;

                    try
                    {
                        // Update IsSelected based on status
                        bool shouldBeSelected = status == RegistrySettingStatus.Applied;

                        // Set the checkbox state to match the registry state
                        _logService.Log(LogLevel.Info, $"Setting {setting.Name} status is {status}, setting IsSelected to {shouldBeSelected}");
                        setting.IsSelected = shouldBeSelected;
                    }
                    finally
                    {
                        // Reset the flag
                        setting.IsUpdatingFromCode = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking Start Menu setting statuses: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the status message for a setting.
        /// </summary>
        /// <param name="setting">The setting.</param>
        /// <returns>The status message.</returns>
        private string GetStatusMessage(ApplicationSettingItem setting)
        {
            // Get status
            var status = setting.Status;
            string message = status switch
            {
                RegistrySettingStatus.Applied => "Setting is enabled (toggle is ON)",
                RegistrySettingStatus.NotApplied => "Setting is disabled (toggle is OFF)",
                RegistrySettingStatus.Modified => "Setting has a custom value different from both enabled and disabled values",
                RegistrySettingStatus.Error => "Error checking setting status",
                _ => "Unknown status"
            };

            // Add current value if available
            var currentValue = setting.CurrentValue;
            if (currentValue != null)
            {
                message += $"\nCurrent value: {currentValue}";
            }

            // Add enabled value if available
            var registrySetting = setting.RegistrySetting;
            object? enabledValue = registrySetting?.EnabledValue ?? registrySetting?.RecommendedValue;
            if (enabledValue != null)
            {
                message += $"\nEnabled value (ON): {enabledValue}";
            }

            // Add disabled value if available
            object? disabledValue = registrySetting?.DisabledValue ?? registrySetting?.DefaultValue;
            if (disabledValue != null)
            {
                message += $"\nDisabled value (OFF): {disabledValue}";
            }

            return message;
        }
    }
}
