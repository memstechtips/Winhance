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

using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Start Menu customizations.
    /// </summary>
    public partial class StartMenuCustomizationsViewModel : BaseSettingsViewModel<ApplicationSettingItem>
    {
        private readonly ISystemServices _systemServices;
        private readonly IDialogService _dialogService;
        private readonly IScheduledTaskService _scheduledTaskService;
        private bool _isWindows11;

        /// <summary>
        /// Gets the collection of ComboBox settings.
        /// </summary>
        public ObservableCollection<ApplicationSettingItem> ComboBoxSettings { get; } = new ObservableCollection<ApplicationSettingItem>();

        /// <summary>
        /// Gets the collection of Toggle settings.
        /// </summary>
        public ObservableCollection<ApplicationSettingItem> ToggleSettings { get; } = new ObservableCollection<ApplicationSettingItem>();

        /// <summary>
        /// Gets a value indicating whether there are ComboBox settings.
        /// </summary>
        public bool HasComboBoxSettings => ComboBoxSettings.Count > 0;

        /// <summary>
        /// Gets the command to clean the Start Menu.
        /// </summary>
        [RelayCommand]
        public async Task CleanStartMenu()
        {
            try
            {
                // Show options dialog to get user confirmation
                var confirmed = Winhance.WPF.Features.Customize.Views.StartMenuCleaningOptionsDialog.ShowOptionsDialog();
        
                // If user cancelled, exit early
                if (!confirmed)
                {
                    return;
                }

                // Determine Windows version
                _isWindows11 = _systemServices.IsWindows11();

                // Start progress tracking
                _progressService.StartTask("Cleaning Start Menu...");
                _progressService.UpdateDetailedProgress(new TaskProgressDetail
                {
                    StatusText = "Cleaning Start Menu and applying recommended settings...",
                    Progress = 0
                });
        
                // Clean the Start Menu (always applies to all users)
                await Task.Run(() => StartMenuCustomizations.CleanStartMenu(_isWindows11, _systemServices, _logService, _scheduledTaskService));
        
                // Apply comprehensive recommended settings based on Windows version
                await ApplyRecommendedStartMenuSettings();

                // Log success
                _logService.Log(LogLevel.Info, $"Start Menu cleaned successfully");

                // Update completion progress
                _progressService.UpdateDetailedProgress(new TaskProgressDetail
                {
                    StatusText = "Start Menu cleaned successfully",
                    Progress = 100
                });
                
                // Complete the task
                _progressService.CompleteTask();

                // Show success dialog with appropriate message
                var successMessage = "Start Menu has been cleaned successfully for all users. Recommended privacy settings have been applied to disable suggestions, recommendations, and tracking features. Current user changes are immediate, other users will see changes on next login.";
            
                await _dialogService.ShowInformationAsync(successMessage, "Start Menu Cleaned");
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

                // Show error dialog
                await _dialogService.ShowErrorAsync(
                    $"Failed to clean Start Menu: {ex.Message}",
                    "Start Menu Cleaning Failed");
            }
        }

        /// <summary>
        /// Applies comprehensive recommended Start Menu settings based on Windows version.
        /// </summary>
        private async Task ApplyRecommendedStartMenuSettings()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Applying recommended Start Menu settings");
                
                // Get all Start Menu customizations
                var startMenuCustomizations = Core.Features.Customize.Models.StartMenuCustomizations.GetStartMenuCustomizations();
                
                if (_isWindows11)
                {
                    await ApplyWindows11RecommendedSettings(startMenuCustomizations);
                }
                else
                {
                    await ApplyWindows10RecommendedSettings(startMenuCustomizations);
                }
                
                _logService.Log(LogLevel.Info, "Completed applying recommended Start Menu settings");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying recommended Start Menu settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies Windows 11 specific recommended settings.
        /// </summary>
        private async Task ApplyWindows11RecommendedSettings(CustomizationGroup startMenuCustomizations)
        {
            var settingsToApply = new List<(string Id, bool EnabledState, object? ComboBoxValue)>
            {
                ("start-menu-layout", true, 1), // Set to "More pins"
                ("show-recently-added-apps", false, null), // Disable
                ("start-track-progs", false, null), // Disable Show Most Used Apps
                ("show-recommended-files", false, null), // Disable Show Recommended & Recently Opened Items
                ("start-menu-recommendations", false, null), // Disable Show Recommended Tips, Shortcuts etc.
                ("show-account-notifications", false, null), // Disable Show Account-related Notifications
                ("display-bing-search-results", false, null), // Disable Display Bing Search Results
                ("remove-recommended-section", true, null) // Enable Remove Recommended Section
            };

            foreach (var (settingId, enabledState, comboBoxValue) in settingsToApply)
            {
                await ApplyIndividualSetting(startMenuCustomizations, settingId, enabledState, comboBoxValue);
            }
        }

        /// <summary>
        /// Applies Windows 10 specific recommended settings.
        /// </summary>
        private async Task ApplyWindows10RecommendedSettings(CustomizationGroup startMenuCustomizations)
        {
            var settingsToApply = new List<(string Id, bool EnabledState, object? ComboBoxValue)>
            {
                ("show-recently-added-apps", false, null), // Disable
                ("start-track-progs", false, null), // Disable Show Most Used Apps
                ("start-menu-suggestions", false, null), // Disable Show suggestions in Start
                ("show-recommended-files", false, null), // Disable Show Recommended & Recently Opened Items
                ("show-account-notifications", false, null), // Disable Show Account-related Notifications
                ("display-bing-search-results", false, null) // Disable Display Bing Search Results
            };

            foreach (var (settingId, enabledState, comboBoxValue) in settingsToApply)
            {
                await ApplyIndividualSetting(startMenuCustomizations, settingId, enabledState, comboBoxValue);
            }
        }

        /// <summary>
        /// Applies an individual setting by ID.
        /// </summary>
        private async Task ApplyIndividualSetting(CustomizationGroup startMenuCustomizations, string settingId, bool enabledState, object? comboBoxValue)
        {
            try
            {
                var setting = startMenuCustomizations?.Settings?.FirstOrDefault(s => s.Id == settingId);
                if (setting?.RegistrySettings == null)
                {
                    _logService.Log(LogLevel.Warning, $"Setting '{settingId}' not found or has no registry settings");
                    return;
                }

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    // For ComboBox settings, we need to temporarily set the appropriate value
                    if (comboBoxValue != null)
                    {
                        // Create a temporary registry setting with the ComboBox value
                        var tempSetting = registrySetting with 
                        {
                            EnabledValue = comboBoxValue,
                            DisabledValue = comboBoxValue
                        };
                        
                        var success = await _registryService.ApplySettingAsync(tempSetting, true);
                        
                        if (success)
                        {
                            _logService.Log(LogLevel.Info, $"Applied ComboBox setting '{settingId}': {registrySetting.SubKey}\\{registrySetting.Name} = {comboBoxValue}");
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"Failed to apply ComboBox setting '{settingId}': {registrySetting.SubKey}\\{registrySetting.Name}");
                        }
                    }
                    else
                    {
                        // For toggle settings, use the registry service's ApplySettingAsync method
                        // This will properly handle Group Policy settings by deleting values when disabling
                        var success = await _registryService.ApplySettingAsync(registrySetting, enabledState);
                        
                        if (success)
                        {
                            var valueApplied = enabledState ? registrySetting.EnabledValue : 
                                             (registrySetting.IsGroupPolicy && !enabledState ? "[DELETED]" : registrySetting.DisabledValue);
                            _logService.Log(LogLevel.Info, $"Applied setting '{settingId}': {registrySetting.SubKey}\\{registrySetting.Name} = {valueApplied}");
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"Failed to apply setting '{settingId}': {registrySetting.SubKey}\\{registrySetting.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying setting '{settingId}': {ex.Message}");
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
                    // Use the registry service's ApplySettingAsync method to properly handle Group Policy settings
                    var tempSetting = action.RegistrySetting with 
                    {
                        EnabledValue = action.RegistrySetting.RecommendedValue,
                        DisabledValue = action.RegistrySetting.DefaultValue
                    };
                    
                    await _registryService.ApplySettingAsync(tempSetting, true);
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
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="scheduledTaskService">The scheduled task service.</param>
        public StartMenuCustomizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            ISystemServices systemServices,
            IDialogService dialogService,
            IScheduledTaskService scheduledTaskService)
            : base(progressService, registryService, logService)
        {
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _scheduledTaskService = scheduledTaskService ?? throw new ArgumentNullException(nameof(scheduledTaskService));
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
                ComboBoxSettings.Clear();
                ToggleSettings.Clear();

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

                        // Check build number compatibility
                        if (!IsSettingSupportedOnCurrentBuild(setting))
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
                            // Linked registry settings - use the proper method that respects LinkedSettingsLogic
                            settingItem.LinkedRegistrySettings = setting.CreateLinkedRegistrySettings();
                            _logService.Log(LogLevel.Info, $"Setting up linked registry settings for {setting.Name} with {setting.RegistrySettings.Count} entries and logic {setting.LinkedSettingsLogic}");
                            
                            // Log details about each registry entry for debugging
                            foreach (var regSetting in setting.RegistrySettings)
                            {
                                _logService.Log(LogLevel.Info, $"Adding linked registry setting for {setting.Name}: {regSetting.Hive}\\{regSetting.SubKey}\\{regSetting.Name}");
                            }
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"No registry settings found for {setting.Name}");
                        }

                        // Set up ComboBox options if this is a ComboBox control
                        if (setting.ControlType == ControlType.ComboBox)
                        {
                            SetupComboBoxOptions(settingItem, setting);
                            ComboBoxSettings.Add(settingItem);
                        }
                        else
                        {
                            ToggleSettings.Add(settingItem);
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
                                
                                // Update dynamic tooltips when "Remove Recommended Section" changes
                                if (setting.Id == "remove-recommended-section")
                                {
                                    UpdateDynamicTooltips();
                                }
                            }
                        };
                    }
                }

                // Notify property changes
                OnPropertyChanged(nameof(HasComboBoxSettings));

                // Check setting statuses
                await CheckSettingStatusesAsync();
                
                // Initialize dynamic tooltips after settings are loaded
                UpdateDynamicTooltips();
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
                    RegistrySettingStatus status;
                    object currentValue = null;
                    
                    // Handle linked registry settings (multiple registry entries)
                    if (setting.LinkedRegistrySettings != null && setting.LinkedRegistrySettings.Settings.Count > 0)
                    {
                        _logService.Log(LogLevel.Info, $"Checking status for linked setting: {setting.Name} with {setting.LinkedRegistrySettings.Settings.Count} registry entries");
                        
                        // Get the combined status of all linked settings using the registry service
                        status = await _registryService.GetLinkedSettingsStatusAsync(setting.LinkedRegistrySettings);
                        setting.Status = status;
                        
                        // For current value display, use the first setting's value
                        if (setting.LinkedRegistrySettings.Settings.Count > 0)
                        {
                            var firstSetting = setting.LinkedRegistrySettings.Settings[0];
                            currentValue = await _registryService.GetCurrentValueAsync(firstSetting);
                            setting.CurrentValue = currentValue;
                            
                            // Check for null registry values and populate LinkedRegistrySettingsWithValues
                            bool anyNull = false;
                            setting.LinkedRegistrySettingsWithValues.Clear();
                            
                            foreach (var regSetting in setting.LinkedRegistrySettings.Settings)
                            {
                                var regCurrentValue = await _registryService.GetCurrentValueAsync(regSetting);
                                if (regCurrentValue == null)
                                {
                                    anyNull = true;
                                }
                                setting.LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(regSetting, regCurrentValue));
                            }
                            
                            // Set IsRegistryValueNull for linked settings
                            setting.IsRegistryValueNull = anyNull;
                        }
                        else
                        {
                            currentValue = null;
                        }
                    }
                    // Handle single registry settings
                    else if (setting.RegistrySetting != null)
                    {
                        // Get status for single registry setting
                        status = await _registryService.GetSettingStatusAsync(setting.RegistrySetting);
                        setting.Status = status;

                        // Get current value for single registry setting
                        currentValue = await _registryService.GetCurrentValueAsync(setting.RegistrySetting);
                        setting.CurrentValue = currentValue;
                    }
                    else
                    {
                        // No registry settings configured
                        _logService.Log(LogLevel.Warning, $"Setting {setting.Name} has no registry settings configured");
                        status = RegistrySettingStatus.Unknown;
                        setting.Status = status;
                        currentValue = null;
                        setting.CurrentValue = currentValue;
                    }

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
        /// Sets up ComboBox options from the setting's CustomProperties.
        /// </summary>
        /// <param name="settingItem">The setting item to configure.</param>
        /// <param name="setting">The source customization setting.</param>
        private void SetupComboBoxOptions(ApplicationSettingItem settingItem, CustomizationSetting setting)
        {
            if (setting.RegistrySettings.Count > 0 && 
                setting.RegistrySettings[0].CustomProperties != null &&
                setting.RegistrySettings[0].CustomProperties.TryGetValue("ComboBoxOptions", out var optionsObj) &&
                optionsObj is Dictionary<string, int> options)
            {
                // Create ComboBox options collection
                var comboBoxOptions = new ObservableCollection<string>(options.Keys);
                settingItem.ComboBoxOptions = comboBoxOptions;

                // Read current registry value and map to correct option
                var registrySetting = setting.RegistrySettings[0];
                string hiveString = GetRegistryHiveString(registrySetting.Hive);
                var currentValue = _registryService.GetValue(
                    $"{hiveString}\\{registrySetting.SubKey}",
                    registrySetting.Name);

                // Convert current registry value to int for comparison
                int currentValueInt = currentValue switch
                {
                    int intValue => intValue,
                    long longValue => (int)longValue,
                    null => registrySetting.DefaultValue is int defaultInt ? defaultInt : 0, // Default to 0 (Default layout)
                    _ => registrySetting.DefaultValue is int defaultInt2 ? defaultInt2 : 0
                };

                // Find the option name that corresponds to this registry value
                var matchingOption = options.FirstOrDefault(kvp => kvp.Value == currentValueInt);
                
                if (!string.IsNullOrEmpty(matchingOption.Key))
                {
                    settingItem.SelectedValue = matchingOption.Key;
                    _logService.Log(LogLevel.Info, $"Set ComboBox {setting.Name} to '{matchingOption.Key}' based on current registry value {currentValueInt}");
                }
                else
                {
                    // Fallback to default option if no match found
                    if (setting.RegistrySettings[0].CustomProperties.TryGetValue("DefaultOption", out var defaultObj) &&
                        defaultObj is string defaultOption &&
                        options.ContainsKey(defaultOption))
                    {
                        settingItem.SelectedValue = defaultOption;
                    }
                    else if (comboBoxOptions.Count > 0)
                    {
                        settingItem.SelectedValue = comboBoxOptions[0];
                    }
                    _logService.Log(LogLevel.Warning, $"No matching option found for registry value {currentValueInt} in {setting.Name}, using default");
                }

                _logService.Log(LogLevel.Info, $"Set up ComboBox for {setting.Name} with {options.Count} options: {string.Join(", ", options.Keys)}");
            }
            else
            {
                _logService.Log(LogLevel.Warning, $"No ComboBoxOptions found in CustomProperties for {setting.Name}");
            }
        }

        /// <summary>
        /// Gets the registry hive string.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <returns>The registry hive string.</returns>
        private string GetRegistryHiveString(RegistryHive hive)
        {
            return hive switch
            {
                RegistryHive.ClassesRoot => "HKCR",
                RegistryHive.CurrentUser => "HKCU",
                RegistryHive.LocalMachine => "HKLM",
                RegistryHive.Users => "HKU",
                RegistryHive.CurrentConfig => "HKCC",
                _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, null)
            };
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

        /// <summary>
        /// Checks if a setting is supported on the current Windows build.
        /// </summary>
        /// <param name="setting">The customization setting to check.</param>
        /// <returns>True if the setting is supported on the current build, false otherwise.</returns>
        private bool IsSettingSupportedOnCurrentBuild(CustomizationSetting setting)
        {
            // Get current Windows version info
            var versionInfo = _systemServices.GetType()
                .GetMethod("GetWindowsVersionInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_systemServices, null);
            
            if (versionInfo == null)
            {
                _logService.Log(LogLevel.Warning, "Could not get Windows version info for build filtering");
                return true; // Default to showing the setting if we can't determine version
            }

            var buildNumberProperty = versionInfo.GetType().GetProperty("BuildNumber");
            if (buildNumberProperty?.GetValue(versionInfo) is not int currentBuild)
            {
                _logService.Log(LogLevel.Warning, "Could not get current build number for filtering");
                return true; // Default to showing the setting if we can't determine build
            }

            _logService.Log(LogLevel.Info, $"Checking build compatibility for {setting.Name} - Current build: {currentBuild}");

            // Check SupportedBuildRanges first (takes precedence)
            if (setting.SupportedBuildRanges != null && setting.SupportedBuildRanges.Count > 0)
            {
                bool isInRange = setting.SupportedBuildRanges.Any(range => 
                    currentBuild >= range.MinBuild && currentBuild <= range.MaxBuild);
                
                _logService.Log(LogLevel.Info, 
                    $"Setting {setting.Name} has build ranges: {string.Join(", ", setting.SupportedBuildRanges.Select(r => $"[{r.MinBuild}-{r.MaxBuild}]"))} - Supported: {isInRange}");
                
                return isInRange;
            }

            // Check MinimumBuildNumber
            if (setting.MinimumBuildNumber.HasValue && currentBuild < setting.MinimumBuildNumber.Value)
            {
                _logService.Log(LogLevel.Info, 
                    $"Setting {setting.Name} requires minimum build {setting.MinimumBuildNumber.Value}, current is {currentBuild} - Not supported");
                return false;
            }

            // Check MaximumBuildNumber
            if (setting.MaximumBuildNumber.HasValue && currentBuild > setting.MaximumBuildNumber.Value)
            {
                _logService.Log(LogLevel.Info, 
                    $"Setting {setting.Name} supports maximum build {setting.MaximumBuildNumber.Value}, current is {currentBuild} - Not supported");
                return false;
            }

            // If we get here, the setting is supported
            if (setting.MinimumBuildNumber.HasValue || setting.MaximumBuildNumber.HasValue)
            {
                _logService.Log(LogLevel.Info, 
                    $"Setting {setting.Name} is supported on current build {currentBuild}");
            }

            return true;
        }

        /// <summary>
        /// Updates the descriptions of settings that are affected by the "Remove Recommended Section" toggle on Windows 11.
        /// </summary>
        private void UpdateDynamicTooltips()
        {
            if (!_isWindows11)
                return;

            // Find the "Remove Recommended Section" setting
            var removeRecommendedSetting = Settings.FirstOrDefault(s => s.Id == "remove-recommended-section");
            if (removeRecommendedSetting == null)
                return;

            bool isRemoveRecommendedEnabled = removeRecommendedSetting.IsSelected;

            // List of setting IDs that are affected by the "Remove Recommended Section" toggle
            var affectedSettingIds = new List<string>
            {
                "show-recently-added-apps",
                "start-track-progs",
                "show-recommended-files",
                "start-menu-recommendations"
            };

            foreach (var settingId in affectedSettingIds)
            {
                var setting = Settings.FirstOrDefault(s => s.Id == settingId);
                if (setting != null)
                {
                    // Get the original description from the core model
                    var startMenuCustomizations = Core.Features.Customize.Models.StartMenuCustomizations.GetStartMenuCustomizations();
                    var originalSetting = startMenuCustomizations.Settings.FirstOrDefault(s => s.Id == settingId);
                    
                    if (originalSetting != null)
                    {
                        string baseDescription = originalSetting.Description;
                        
                        if (isRemoveRecommendedEnabled)
                        {
                            // Add the warning note when Remove Recommended Section is enabled
                            setting.Description = baseDescription + "\n\nNote: This setting won't have any visual effect until the 'Remove Recommended Section' toggle is disabled.";
                        }
                        else
                        {
                            // Use the original description when Remove Recommended Section is disabled
                            setting.Description = baseDescription;
                        }
                    }
                }
            }
        }
    }
}
