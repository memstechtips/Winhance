using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Base class for application setting items used in both Optimization and Customization features.
    /// </summary>
    public partial class ApplicationSettingItem : ObservableObject, ISettingItem, ISearchable
    {
        private readonly IRegistryService? _registryService;
        private readonly ICommandService? _commandService;
        private readonly IDialogService? _dialogService;
        private readonly ILogService? _logService;
        private readonly IDependencyManager? _dependencyManager;
        private readonly IGlobalSettingsRegistry? _globalSettingsRegistry;
        
        /// <summary>
        /// Flag to indicate if the setting is currently being initialized to prevent PostSettingAction execution during startup.
        /// </summary>
        public bool IsInitializing { get; set; } = true;

        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullName = string.Empty;

        partial void OnNameChanged(string value) => UpdateFullName();

        private void UpdateFullName()
        {
            FullName = Name;
        }

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value)
        {
            // Apply the setting when IsSelected changes (user interaction)
            ApplySetting();
        }

        [ObservableProperty]
        private bool _isGroupHeader;

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private string _groupName = string.Empty;

        [ObservableProperty]
        private RegistrySettingStatus _status = RegistrySettingStatus.Unknown;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private object? _currentValue;

        /// <summary>
        /// Gets a user-friendly display value for the current registry value.
        /// Returns "Key doesn't exist" when the value is null, otherwise returns the actual value.
        /// </summary>
        public string DisplayValue
        {
            get
            {
                if (CurrentValue == null)
                {
                    return "Key doesn't exist";
                }
                return CurrentValue.ToString() ?? "Key doesn't exist";
            }
        }

        [ObservableProperty]
        private object? _selectedValue;

        partial void OnSelectedValueChanged(object? value)
        {
            // Apply the setting when SelectedValue changes for ComboBox controls (user interaction)
            if (ControlType == ControlType.ComboBox)
            {
                ApplySetting();
            }
        }

        [ObservableProperty]
        private bool _isRegistryValueNull;

        [ObservableProperty]
        private ControlType _controlType = ControlType.BinaryToggle;

        [ObservableProperty]
        private int? _sliderSteps;

        [ObservableProperty]
        private int _sliderValue;

        [ObservableProperty]
        private ObservableCollection<string> _sliderLabels = new();

        [ObservableProperty]
        private bool _isApplying;

        [ObservableProperty]
        private ObservableCollection<string> _comboBoxOptions = new();

        [ObservableProperty]
        private string? _icon;

        /// <summary>
        /// Gets or sets the registry setting.
        /// </summary>
        public RegistrySetting? RegistrySetting { get; set; }

        private LinkedRegistrySettings? _linkedRegistrySettings;

        /// <summary>
        /// Gets or sets the linked registry settings.
        /// </summary>
        public LinkedRegistrySettings? LinkedRegistrySettings
        {
            get => _linkedRegistrySettings;
            set
            {
                _linkedRegistrySettings = value;

                // Populate LinkedRegistrySettingsWithValues when LinkedRegistrySettings is assigned
                if (value != null && value.Settings.Count > 0)
                {
                    // Use dispatcher if available (UI thread), otherwise update directly (for unit tests)
                    if (Application.Current?.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            LinkedRegistrySettingsWithValues.Clear();
                            foreach (var setting in value.Settings)
                            {
                                LinkedRegistrySettingsWithValues.Add(
                                    new LinkedRegistrySettingWithValue(setting, null)
                                );
                            }
                        });
                    }
                    else
                    {
                        LinkedRegistrySettingsWithValues.Clear();
                        foreach (var setting in value.Settings)
                        {
                            LinkedRegistrySettingsWithValues.Add(
                                new LinkedRegistrySettingWithValue(setting, null)
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the linked registry settings with values.
        /// </summary>
        public ObservableCollection<LinkedRegistrySettingWithValue> LinkedRegistrySettingsWithValues { get; set; } =
            new();

        /// <summary>
        /// Gets or sets the command settings.
        /// </summary>
        public List<CommandSetting> CommandSettings { get; set; } = new List<CommandSetting>();

        /// <summary>
        /// Gets or sets the dependencies between settings.
        /// </summary>
        public List<SettingDependency> Dependencies { get; set; } = new List<SettingDependency>();

        /// <summary>
        /// Gets or sets the callback action to execute after the setting is applied.
        /// This can be used for post-setting actions like restarting processes.
        /// </summary>
        public Action? PostSettingAction { get; set; }

        /// <summary>
        /// Gets or sets the dropdown options.
        /// </summary>
        public ObservableCollection<string> DropdownOptions { get; set; } = new();

        /// <summary>
        /// Gets or sets the selected dropdown option.
        /// </summary>
        [ObservableProperty]
        private string _selectedDropdownOption = string.Empty;

        /// <summary>
        /// Gets a value indicating whether there are no settings to display.
        /// </summary>
        public bool HasNoSettings
        {
            get
            {
                // True if there are no registry settings and no command settings
                bool hasRegistrySettings =
                    RegistrySetting != null
                    || (
                        LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0
                    );
                bool hasCommandSettings = CommandSettings != null && CommandSettings.Count > 0;

                return !hasRegistrySettings && !hasCommandSettings;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this setting only has command settings (no registry settings).
        /// </summary>
        public bool HasCommandSettingsOnly
        {
            get
            {
                // True if there are command settings but no registry settings
                bool hasRegistrySettings =
                    RegistrySetting != null
                    || (
                        LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0
                    );
                bool hasCommandSettings = CommandSettings != null && CommandSettings.Count > 0;

                return hasCommandSettings && !hasRegistrySettings;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this is a grouped setting that contains child settings.
        /// </summary>
        public bool IsGroupedSetting { get; set; }

        /// <summary>
        /// Gets or sets the child settings for a grouped setting.
        /// </summary>
        public ObservableCollection<ApplicationSettingItem> ChildSettings { get; set; } =
            new ObservableCollection<ApplicationSettingItem>();

        /// <summary>
        /// Gets or sets a dictionary of custom properties.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } =
            new Dictionary<string, object>();

        /// <summary>
        /// Gets the collection of actions associated with this setting.
        /// </summary>
        public List<Winhance.Core.Features.Common.Models.ApplicationAction> Actions { get; } =
            new List<Winhance.Core.Features.Common.Models.ApplicationAction>();

        /// <summary>
        /// Gets or sets the command to apply the setting.
        /// </summary>
        public ICommand ApplySettingCommand { get; private set; }

        /// <summary>
        /// Gets or sets the command to restore the setting to its default value.
        /// </summary>
        public ICommand RestoreDefaultCommand { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this setting is only for Windows 11.
        /// </summary>
        public bool IsWindows11Only { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this setting is only for Windows 10.
        /// </summary>
        public bool IsWindows10Only { get; set; }

        /// <summary>
        /// Gets or sets the setting type.
        /// </summary>
        public string SettingType { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationSettingItem"/> class.
        /// </summary>
        public ApplicationSettingItem()
        {
            ApplySettingCommand = new RelayCommand(ApplySetting);
            RestoreDefaultCommand = new RelayCommand(RestoreDefault);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationSettingItem"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="commandService">The command service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        /// <param name="globalSettingsRegistry">The global settings registry.</param>
        public ApplicationSettingItem(
            IRegistryService? registryService,
            IDialogService? dialogService,
            ILogService? logService,
            ICommandService? commandService = null,
            IDependencyManager? dependencyManager = null,
            IGlobalSettingsRegistry? globalSettingsRegistry = null
        )
            : this()
        {
            _registryService = registryService;
            _dialogService = dialogService;
            _logService = logService;
            _commandService = commandService;
            _dependencyManager = dependencyManager;
            _globalSettingsRegistry = globalSettingsRegistry;
        }

        /// <summary>
        /// Updates the UI state from the current registry values without triggering property change events.
        /// This should only be used during initialization or refresh operations.
        /// </summary>
        /// <param name="isSelected">The current selection state from registry.</param>
        /// <param name="selectedValue">The current selected value from registry (for ComboBox controls).</param>
        /// <param name="status">The current registry status.</param>
        /// <param name="currentValue">The current registry value.</param>
        public void UpdateUIStateFromRegistry(bool isSelected, object? selectedValue = null, RegistrySettingStatus status = RegistrySettingStatus.Unknown, object? currentValue = null)
        {
            // Directly set the backing fields to avoid triggering property change events
            SetProperty(ref _isSelected, isSelected, nameof(IsSelected));
            
            if (selectedValue != null)
            {
                SetProperty(ref _selectedValue, selectedValue, nameof(SelectedValue));
            }
            
            SetProperty(ref _status, status, nameof(Status));
            SetProperty(ref _currentValue, currentValue, nameof(CurrentValue));
            
            // Update status message
            StatusMessage = GetStatusMessage(status);
            
            // Notify that DisplayValue might have changed
            OnPropertyChanged(nameof(DisplayValue));
        }
        
        /// <summary>
        /// Gets the status message for a given status.
        /// </summary>
        /// <param name="status">The registry setting status.</param>
        /// <returns>A user-friendly status message.</returns>
        private string GetStatusMessage(RegistrySettingStatus status)
        {
            return status switch
            {
                RegistrySettingStatus.Applied => "Setting is applied with recommended value",
                RegistrySettingStatus.NotApplied => "Setting is not applied or using default value",
                RegistrySettingStatus.Modified => "Setting has a custom value different from recommended",
                RegistrySettingStatus.Error => "Error checking setting status",
                _ => "Unknown status"
            };
        }

        /// <summary>
        /// Applies the setting.
        /// </summary>
        public async void ApplySetting()
        {
            // Check dependencies before applying if the setting is being enabled
            if (IsSelected && Dependencies?.Any() == true && _dependencyManager != null)
            {
                try
                {
                    var allSettings = _globalSettingsRegistry?.GetAllSettings() ?? new List<ISettingItem>();
                    var unsatisfiedDependencies = _dependencyManager.GetUnsatisfiedDependencies(Id, allSettings);
                    
                    if (unsatisfiedDependencies.Any())
                    {
                        _logService?.Log(LogLevel.Info, $"Found {unsatisfiedDependencies.Count} unsatisfied dependencies for '{Name}'");
                        
                        // Show confirmation dialog if available
                        if (_dialogService != null)
                        {
                            var settingNames = unsatisfiedDependencies.Select(s => s.Name).ToList();
                            var headerText = $"To enable '{Name}', the following settings must also be enabled:";
                            var footerText = "Would you like to continue and enable these settings automatically?";
                            var title = "Dependency Confirmation";
                            
                            // Use CustomDialog for better user experience (like Clean Start Menu dialog)
                            bool? userConfirmed = Winhance.WPF.Features.Common.Views.CustomDialog.ShowConfirmation(
                                title, headerText, settingNames, footerText);
                            
                            if (userConfirmed != true)
                            {
                                // User cancelled, revert the toggle using UI state update to avoid recursion
                                _logService?.Log(LogLevel.Info, $"User cancelled dependency confirmation for '{Name}'");
                                UpdateUIStateFromRegistry(false, SelectedValue, Status, CurrentValue);
                                return;
                            }
                        }
                        
                        // Enable dependencies automatically using the new method that handles ComboBox values
                        _logService?.Log(LogLevel.Info, $"Enabling {unsatisfiedDependencies.Count} dependencies for '{Name}'");
                        bool dependenciesEnabled = _dependencyManager.EnableDependenciesForSetting(Id, allSettings);
                        
                        if (!dependenciesEnabled)
                        {
                            _logService?.Log(LogLevel.Warning, $"Failed to enable some dependencies for '{Name}'");
                            // Could show an error dialog here if needed
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService?.Log(LogLevel.Error, $"Error checking dependencies for '{Name}': {ex.Message}");
                    // Continue with applying the setting even if dependency check fails
                }
            }

            // Apply registry settings if available
            if (_registryService != null)
            {
                await Task.Run(ApplyRegistrySettings);
            }

            // Apply command settings if available
            if (_commandService != null && CommandSettings.Any())
            {
                await ApplyCommandSettingsAsync();
            }
            
            // Handle dependency management after applying the setting
            if (_dependencyManager != null && _globalSettingsRegistry != null)
            {
                try
                {
                    var allSettings = _globalSettingsRegistry.GetAllSettings();
                    
                    if (!IsSelected)
                    {
                        // Setting was disabled, check if any dependent settings need to be disabled
                        _dependencyManager.HandleSettingDisabled(Id, allSettings);
                    }
                    else if (ControlType == ControlType.ComboBox)
                    {
                        // ComboBox value changed, check if dependent settings with RequiresSpecificValue need to be disabled
                        _dependencyManager.HandleSettingValueChanged(Id, allSettings);
                    }
                }
                catch (Exception ex)
                {
                    _logService?.Log(LogLevel.Error, $"Error handling dependencies after applying '{Name}': {ex.Message}");
                }
            }
            
            // Execute post-setting action if available
            try
            {
                PostSettingAction?.Invoke();
            }
            catch (Exception ex)
            {
                _logService?.Log(LogLevel.Error, $"Error executing post-setting action for '{Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the registry settings.
        /// </summary>
        private async void ApplyRegistrySettings()
        {
            if (_registryService == null)
            {
                return;
            }

            // Apply the setting using the registry service's ApplySettingAsync method
            // This ensures Group Policy settings are handled properly
            if (RegistrySetting != null)
            {
                try
                {
                    bool success;

                    if (ControlType == ControlType.ComboBox)
                    {
                        // For ComboBox settings, create a temporary setting with the selected value
                        var comboBoxValue = GetComboBoxRegistryValue();
                        var tempSetting = RegistrySetting with
                        {
                            EnabledValue = comboBoxValue,
                            DisabledValue = comboBoxValue,
                        };

                        success = await _registryService.ApplySettingAsync(tempSetting, true);

                        if (success)
                        {
                            CurrentValue = comboBoxValue;
                            _logService?.Log(
                                LogLevel.Info,
                                $"Applied ComboBox setting {Name}: {SelectedValue} (Registry Value: {comboBoxValue})"
                            );
                        }
                    }
                    else
                    {
                        // For binary toggle settings, use the registry service's ApplySettingAsync method
                        success = await _registryService.ApplySettingAsync(
                            RegistrySetting,
                            IsSelected
                        );

                        // Clear registry cache to ensure we read fresh values (fixes cache key mismatch between delete and read operations)
                        _registryService.ClearRegistryCaches();

                        if (success)
                        {
                            // Read the actual current value from the registry after applying changes
                            // This ensures we get the real registry state, including null for deleted Group Policy values
                            var registryPath = GetRegistryHiveString(RegistrySetting.Hive) + "\\" + RegistrySetting.SubKey;
                            CurrentValue = _registryService.GetValue(registryPath, RegistrySetting.Name);

                            _logService?.Log(
                                LogLevel.Info,
                                $"Applied setting {Name}: {(IsSelected ? "Enabled" : "Disabled")}"
                            );
                        }
                    }

                    if (success)
                    {
                        // Add a small delay to ensure registry changes are fully committed
                        await Task.Delay(100);

                        // Update the linked registry settings with values collection on UI thread
                        if (Application.Current?.Dispatcher != null)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                LinkedRegistrySettingsWithValues.Clear();
                                LinkedRegistrySettingsWithValues.Add(
                                    new LinkedRegistrySettingWithValue(
                                        RegistrySetting,
                                        CurrentValue
                                    )
                                );
                            });
                        }
                        else
                        {
                            LinkedRegistrySettingsWithValues.Clear();
                            LinkedRegistrySettingsWithValues.Add(
                                new LinkedRegistrySettingWithValue(RegistrySetting, CurrentValue)
                            );
                        }

                        // Update status
                        Status = RegistrySettingStatus.Applied;
                        StatusMessage = "Applied";
                    }
                    else
                    {
                        Status = RegistrySettingStatus.Error;
                        StatusMessage = "Failed to apply";
                        _logService?.Log(LogLevel.Error, $"Failed to apply setting {Name}");
                    }
                }
                catch (Exception ex)
                {
                    Status = RegistrySettingStatus.Error;
                    StatusMessage = "Error occurred";
                    _logService?.Log(
                        LogLevel.Error,
                        $"Error applying setting {Name}: {ex.Message}"
                    );
                }
            }
            else if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
            {
                try
                {
                    bool success;
                    
                    if (ControlType == ControlType.ComboBox)
                    {
                        // For ComboBox controls with linked settings, determine if we should enable or disable
                        var comboBoxValue = GetComboBoxRegistryValue();
                        success = true; // Start with true, set to false if any fail
                        
                        foreach (var setting in LinkedRegistrySettings.Settings)
                        {
                            // For Group Policy settings, use the Group Policy logic:
                            // - If comboBoxValue is 0 and IsGroupPolicy = true, call with isEnabled = false (triggers deletion)
                            // - If comboBoxValue is 1, call with isEnabled = true (sets the value)
                            bool isEnabled;
                            if (setting.IsGroupPolicy && comboBoxValue is int intValue && intValue == 0)
                            {
                                // "Show" selected (value 0) - disable Group Policy (delete registry values)
                                isEnabled = false;
                            }
                            else
                            {
                                // "Hide" selected (value 1) - enable Group Policy (set registry values)
                                isEnabled = true;
                            }
                            
                            var individualSuccess = await _registryService.ApplySettingAsync(setting, isEnabled);
                            if (!individualSuccess)
                            {
                                success = false;
                            }
                        }
                        
                        if (success)
                        {
                            _logService?.Log(
                                LogLevel.Info,
                                $"Applied linked ComboBox settings for {Name}: {SelectedValue} (Registry Value: {comboBoxValue})"
                            );
                        }
                    }
                    else
                    {
                        // For binary toggle controls, use the standard linked settings approach
                        success = await _registryService.ApplyLinkedSettingsAsync(
                            LinkedRegistrySettings,
                            IsSelected
                        );
                    }

                    // Clear registry cache to ensure we read fresh values (fixes cache key mismatch between delete and read operations)
                    _registryService.ClearRegistryCaches();

                    // Always update tooltip collection after any toggle operation to reflect current registry state
                    // Add a small delay to ensure registry changes are fully committed
                    await Task.Delay(100);

                    // Get the current values after applying (on background thread)
                    var updatedValues = new List<LinkedRegistrySettingWithValue>();
                    foreach (var setting in LinkedRegistrySettings.Settings)
                    {
                        // Get the current value after applying
                        var registryPath =
                            GetRegistryHiveString(setting.Hive) + "\\" + setting.SubKey;
                        var currentValue = _registryService.GetValue(registryPath, setting.Name);

                        updatedValues.Add(
                            new LinkedRegistrySettingWithValue(setting, currentValue)
                        );
                    }

                    // Update the linked registry settings with values collection on UI thread
                    if (Application.Current?.Dispatcher != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            LinkedRegistrySettingsWithValues.Clear();
                            foreach (var value in updatedValues)
                            {
                                LinkedRegistrySettingsWithValues.Add(value);
                            }
                        });
                    }
                    else
                    {
                        LinkedRegistrySettingsWithValues.Clear();
                        foreach (var value in updatedValues)
                        {
                            LinkedRegistrySettingsWithValues.Add(value);
                        }
                    }

                    if (success)
                    {
                        // Update status
                        Status = IsSelected
                            ? RegistrySettingStatus.Applied
                            : RegistrySettingStatus.NotApplied;
                        StatusMessage =
                            Status == RegistrySettingStatus.Applied ? "Applied" : "Not Applied";

                        _logService?.Log(
                            LogLevel.Info,
                            $"Applied linked settings for {Name}: {(IsSelected ? "Enabled" : "Disabled")}"
                        );
                    }
                    else
                    {
                        Status = RegistrySettingStatus.Error;
                        StatusMessage = "Failed to apply";
                        _logService?.Log(
                            LogLevel.Error,
                            $"Failed to apply linked settings for {Name}"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Status = RegistrySettingStatus.Error;
                    StatusMessage = "Error occurred";
                    _logService?.Log(
                        LogLevel.Error,
                        $"Error applying linked settings for {Name}: {ex.Message}"
                    );
                }
            }
        }

        /// <summary>
        /// Applies the command settings.
        /// </summary>
        private async Task ApplyCommandSettingsAsync()
        {
            if (_commandService == null || !CommandSettings.Any())
            {
                return;
            }

            try
            {
                // Apply the command settings based on the toggle state
                var (success, message) = await _commandService.ApplyCommandSettingsAsync(
                    CommandSettings,
                    IsSelected
                );

                if (success)
                {
                    // Update status without changing IsSelected
                    Status = IsSelected
                        ? RegistrySettingStatus.Applied
                        : RegistrySettingStatus.NotApplied;
                    StatusMessage =
                        Status == RegistrySettingStatus.Applied ? "Applied" : "Not Applied";

                    // Log the action
                    _logService?.Log(
                        LogLevel.Info,
                        $"Applied command settings for {Name}: {(IsSelected ? "Enabled" : "Disabled")}"
                    );
                }
                else
                {
                    // Log the error
                    _logService?.Log(
                        LogLevel.Error,
                        $"Error applying command settings for {Name}: {message}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logService?.Log(
                    LogLevel.Error,
                    $"Exception applying command settings for {Name}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Restores the setting to its default value.
        /// </summary>
        public async void RestoreDefault()
        {
            if (_registryService == null)
            {
                return;
            }

            // Note: No longer need to check IsUpdatingFromCode as UI updates now use UpdateUIStateFromRegistry

            // Restore the setting to its default value using ApplySettingAsync
            if (RegistrySetting != null)
            {
                // Create a temporary setting with DefaultValue as both enabled and disabled value
                var tempSetting = RegistrySetting with
                {
                    EnabledValue = RegistrySetting.DefaultValue,
                    DisabledValue = RegistrySetting.DefaultValue,
                };

                await _registryService.ApplySettingAsync(tempSetting, true);

                // Log the action
                _logService?.Log(LogLevel.Info, $"Restored setting {Name} to default value");
            }
            else if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
            {
                // For linked settings, create a temporary LinkedRegistrySettings with default values
                var tempLinkedSettings = new LinkedRegistrySettings
                {
                    Category = LinkedRegistrySettings.Category,
                    Description = LinkedRegistrySettings.Description,
                    Logic = LinkedRegistrySettings.Logic,
                };

                foreach (var setting in LinkedRegistrySettings.Settings)
                {
                    var tempSetting = setting with
                    {
                        EnabledValue = setting.DefaultValue,
                        DisabledValue = setting.DefaultValue,
                    };
                    tempLinkedSettings.AddSetting(tempSetting);
                }

                await _registryService.ApplyLinkedSettingsAsync(tempLinkedSettings, true);

                // Log the action
                _logService?.Log(
                    LogLevel.Info,
                    $"Restored linked settings for {Name} to default values"
                );
            }

            // Update UI state based on status without triggering property change events
            bool shouldBeSelected = Status == RegistrySettingStatus.Applied;
            UpdateUIStateFromRegistry(shouldBeSelected, SelectedValue, Status, CurrentValue);

            // Refresh the status
            _ = RefreshStatus();
        }

        /// <summary>
        /// Refreshes the status of command settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RefreshCommandSettingsStatusAsync()
        {
            if (_commandService == null || !CommandSettings.Any())
            {
                return;
            }

            try
            {
                // For now, we'll assume command settings are not applied by default
                // In the future, this could be enhanced to check the actual system state
                bool isEnabled = false;

                // If there are primary command settings, check their status
                var primarySetting = CommandSettings.FirstOrDefault(s => s.IsPrimary);
                if (primarySetting != null)
                {
                    isEnabled = await _commandService.IsCommandSettingEnabledAsync(primarySetting);
                }

                // Update status
                Status = isEnabled
                    ? RegistrySettingStatus.Applied
                    : RegistrySettingStatus.NotApplied;
                StatusMessage = Status == RegistrySettingStatus.Applied ? "Applied" : "Not Applied";

                // Update UI state based on status without triggering property change events
                bool shouldBeSelected = Status == RegistrySettingStatus.Applied;
                UpdateUIStateFromRegistry(shouldBeSelected, SelectedValue, Status, CurrentValue);
            }
            catch (Exception ex)
            {
                _logService?.Log(
                    LogLevel.Error,
                    $"Error refreshing command settings status for {Name}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Refreshes the status of the setting.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RefreshStatus()
        {
            // Refresh registry settings status if available
            if (_registryService != null)
            {
                await RefreshRegistrySettingsStatusAsync();
            }

            // Refresh command settings status if available
            if (_commandService != null && CommandSettings.Any())
            {
                await RefreshCommandSettingsStatusAsync();
            }
        }

        /// <summary>
        /// Refreshes the status of registry settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RefreshRegistrySettingsStatusAsync()
        {
            if (_registryService == null)
            {
                return;
            }

            // Get the status
            if (RegistrySetting != null)
            {
                // Get the registry hive string
                string hiveString = GetRegistryHiveString(RegistrySetting.Hive);

                // Get the current value
                var currentValue = _registryService.GetValue(
                    $"{hiveString}\\{RegistrySetting.SubKey}",
                    RegistrySetting.Name
                );

                // Update the current value
                // If the value is null but AbsenceMeansEnabled is true, use the EnabledValue for display
                if (
                    currentValue == null
                    && RegistrySetting.AbsenceMeansEnabled
                    && RegistrySetting.EnabledValue != null
                )
                {
                    CurrentValue = RegistrySetting.EnabledValue;
                }
                else
                {
                    CurrentValue = currentValue;
                }

                // Update the linked registry settings with values collection on UI thread
                if (Application.Current?.Dispatcher != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LinkedRegistrySettingsWithValues.Clear();
                        LinkedRegistrySettingsWithValues.Add(
                            new LinkedRegistrySettingWithValue(RegistrySetting, currentValue)
                        );
                    });
                }
                else
                {
                    LinkedRegistrySettingsWithValues.Clear();
                    LinkedRegistrySettingsWithValues.Add(
                        new LinkedRegistrySettingWithValue(RegistrySetting, currentValue)
                    );
                }

                // Determine if the value is null, but don't show as null if AbsenceMeansEnabled is true
                IsRegistryValueNull = currentValue == null && !RegistrySetting.AbsenceMeansEnabled;

                // Determine the status
                if (currentValue == null)
                {
                    // The value doesn't exist
                    Status =
                        RegistrySetting.DefaultValue == null
                            ? RegistrySettingStatus.Applied
                            : RegistrySettingStatus.NotApplied;
                }
                else
                {
                    // Check if it matches the enabled value first
                    if (
                        RegistrySetting.EnabledValue != null
                        && currentValue.Equals(RegistrySetting.EnabledValue)
                    )
                    {
                        Status = RegistrySettingStatus.Applied;
                    }
                    // Then check if it matches the disabled value
                    else if (
                        RegistrySetting.DisabledValue != null
                        && currentValue.Equals(RegistrySetting.DisabledValue)
                    )
                    {
                        Status = RegistrySettingStatus.NotApplied;
                    }
                    // Finally, fall back to recommended value for backward compatibility
                    else if (currentValue.Equals(RegistrySetting.RecommendedValue))
                    {
                        // If RecommendedValue equals EnabledValue, mark as Applied
                        // If RecommendedValue equals DisabledValue, mark as NotApplied
                        if (
                            RegistrySetting.EnabledValue != null
                            && RegistrySetting.RecommendedValue.Equals(RegistrySetting.EnabledValue)
                        )
                        {
                            Status = RegistrySettingStatus.Applied;
                        }
                        else
                        {
                            Status = RegistrySettingStatus.NotApplied;
                        }
                    }
                    else
                    {
                        Status = RegistrySettingStatus.NotApplied;
                    }
                }

                // Update the status message
                StatusMessage = Status == RegistrySettingStatus.Applied ? "Applied" : "Not Applied";

                // Note: UI state updates now handled via UpdateUIStateFromRegistry method during initialization
            }
            else if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
            {
                // Clear the existing values on UI thread
                if (Application.Current?.Dispatcher != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LinkedRegistrySettingsWithValues.Clear();
                    });
                }
                else
                {
                    LinkedRegistrySettingsWithValues.Clear();
                }

                // Check all linked settings
                bool allApplied = true;
                bool anyApplied = false;
                bool allNull = true;
                bool anyNull = false;

                foreach (var setting in LinkedRegistrySettings.Settings)
                {
                    // Get the registry hive string
                    string hiveString = GetRegistryHiveString(setting.Hive);

                    // Special handling for Remove action type
                    bool isRemoveAction = setting.ActionType == RegistryActionType.Remove;

                    // Get the current value
                    var currentValue = _registryService.GetValue(
                        $"{hiveString}\\{setting.SubKey}",
                        setting.Name
                    );

                    // Add to the linked registry settings with values collection on UI thread
                    if (Application.Current?.Dispatcher != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            LinkedRegistrySettingsWithValues.Add(
                                new LinkedRegistrySettingWithValue(setting, currentValue)
                            );
                        });
                    }
                    else
                    {
                        LinkedRegistrySettingsWithValues.Add(
                            new LinkedRegistrySettingWithValue(setting, currentValue)
                        );
                    }

                    // Check if the value is null
                    if (currentValue == null)
                    {
                        anyNull = true;
                    }
                    else
                    {
                        allNull = false;
                    }

                    // Determine if the value is applied
                    bool isApplied;

                    // For Remove action type, null means the key/value doesn't exist, which means it's applied
                    if (isRemoveAction)
                    {
                        isApplied = currentValue == null;
                    }
                    else if (currentValue == null)
                    {
                        // The value doesn't exist
                        isApplied = setting.DefaultValue == null;
                    }
                    else
                    {
                        // Check if it matches the enabled value first
                        if (
                            setting.EnabledValue != null
                            && currentValue.Equals(setting.EnabledValue)
                        )
                        {
                            isApplied = true;
                        }
                        // Then check if it matches the disabled value
                        else if (
                            setting.DisabledValue != null
                            && currentValue.Equals(setting.DisabledValue)
                        )
                        {
                            isApplied = false;
                        }
                        // Finally, fall back to recommended value for backward compatibility
                        else if (currentValue.Equals(setting.RecommendedValue))
                        {
                            // If RecommendedValue equals EnabledValue, mark as Applied
                            // If RecommendedValue equals DisabledValue, mark as NotApplied
                            if (
                                setting.EnabledValue != null
                                && setting.RecommendedValue.Equals(setting.EnabledValue)
                            )
                            {
                                isApplied = true;
                            }
                            else
                            {
                                isApplied = false;
                            }
                        }
                        else
                        {
                            isApplied = false;
                        }
                    }

                    // Update the status
                    if (isApplied)
                    {
                        anyApplied = true;
                    }
                    else
                    {
                        allApplied = false;
                    }
                }

                // Determine the status based on the logic
                if (LinkedRegistrySettings.Logic == LinkedSettingsLogic.All)
                {
                    // All settings must be applied
                    Status = allApplied
                        ? RegistrySettingStatus.Applied
                        : RegistrySettingStatus.NotApplied;

                    // For ActionType = Remove settings, we need to invert the IsRegistryValueNull logic
                    // because null means the key/value doesn't exist, which is the desired state
                    bool allRemoveActions = LinkedRegistrySettings.Settings.All(s =>
                        s.ActionType == RegistryActionType.Remove
                    );
                    if (allRemoveActions)
                    {
                        // For Remove actions, we want to show the warning when values exist (not null)
                        IsRegistryValueNull = !allNull;
                    }
                    else
                    {
                        IsRegistryValueNull = allNull;
                    }
                }
                else
                {
                    // Any setting must be applied
                    Status = anyApplied
                        ? RegistrySettingStatus.Applied
                        : RegistrySettingStatus.NotApplied;

                    // For ActionType = Remove settings, we need to invert the IsRegistryValueNull logic
                    bool allRemoveActions = LinkedRegistrySettings.Settings.All(s =>
                        s.ActionType == RegistryActionType.Remove
                    );
                    if (allRemoveActions)
                    {
                        // For Remove actions, we want to show the warning when values exist (not null)
                        IsRegistryValueNull = !anyNull;
                    }
                    else
                    {
                        IsRegistryValueNull = anyNull;
                    }
                }

                // Update the status message
                StatusMessage = Status == RegistrySettingStatus.Applied ? "Applied" : "Not Applied";

                // Note: UI state updates now handled via UpdateUIStateFromRegistry method during initialization
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
                RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
                RegistryHive.CurrentUser => "CurrentUser",
                RegistryHive.LocalMachine => "LocalMachine",
                RegistryHive.Users => "HKEY_USERS",
                RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
                _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, null),
            };
        }

        /// <summary>
        /// Determines if the object matches the given search term.
        /// </summary>
        /// <param name="searchTerm">The search term to match against.</param>
        /// <returns>True if the object matches the search term, false otherwise.</returns>
        public virtual bool MatchesSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return true;
            }

            searchTerm = searchTerm.ToLowerInvariant();

            foreach (var propertyName in GetSearchableProperties())
            {
                var property = GetType().GetProperty(propertyName);
                if (property != null)
                {
                    var value = property.GetValue(this)?.ToString();
                    if (
                        !string.IsNullOrWhiteSpace(value)
                        && value.ToLowerInvariant().Contains(searchTerm)
                    )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the searchable properties of the object.
        /// </summary>
        /// <returns>An array of property names that should be searched.</returns>
        public virtual string[] GetSearchableProperties()
        {
            return new[] { nameof(Name), nameof(Description), nameof(GroupName) };
        }

        /// <summary>
        /// Gets the registry value for a ComboBox selection.
        /// </summary>
        /// <returns>The registry value corresponding to the selected ComboBox option.</returns>
        private object GetComboBoxRegistryValue()
        {
            // For linked settings, check the first registry setting for ComboBox options
            var settingToCheck = RegistrySetting ?? LinkedRegistrySettings?.Settings?.FirstOrDefault();
            
            if (
                settingToCheck?.CustomProperties != null
                && settingToCheck.CustomProperties.TryGetValue(
                    "ComboBoxOptions",
                    out var optionsObj
                )
                && !string.IsNullOrEmpty(SelectedValue?.ToString())
            )
            {
                // Handle both Dictionary<string, int> and Dictionary<string, object> formats
                if (optionsObj is Dictionary<string, int> intOptions
                    && intOptions.TryGetValue(SelectedValue.ToString(), out var intValue))
                {
                    return intValue;
                }
                else if (optionsObj is Dictionary<string, object> objectOptions
                    && objectOptions.TryGetValue(SelectedValue.ToString(), out var objectValue))
                {
                    return objectValue; // This can be null, int, or other types
                }
            }

            // Fallback to default value if no mapping found
            return settingToCheck?.DefaultValue ?? 0;
        }
    }
}
