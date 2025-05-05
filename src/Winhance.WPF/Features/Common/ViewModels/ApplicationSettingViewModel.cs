using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Registry;
using Winhance.WPF.Features.Common.Extensions;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Base view model for application settings.
    /// </summary>
    public partial class ApplicationSettingViewModel : ObservableObject, ISettingItem
    {
        protected readonly IRegistryService? _registryService;
        protected readonly IDialogService? _dialogService;
        protected readonly ILogService? _logService;
        protected readonly IDependencyManager? _dependencyManager;
        protected readonly Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService? _powerPlanService;
        protected bool _isUpdatingFromCode;

        /// <summary>
        /// Gets or sets a value indicating whether the IsSelected property is being updated from code.
        /// This is used to prevent automatic application of settings when loading.
        /// </summary>
        public bool IsUpdatingFromCode
        {
            get => _isUpdatingFromCode;
            set => _isUpdatingFromCode = value;
        }

        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private string _groupName = string.Empty;

        [ObservableProperty]
        private bool _isGroupHeader;

        [ObservableProperty]
        private bool _isGroupedSetting;

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private ObservableCollection<ApplicationSettingViewModel> _childSettings = new();

        [ObservableProperty]
        private ControlType _controlType = ControlType.BinaryToggle;

        [ObservableProperty]
        private int? _sliderSteps;

        [ObservableProperty]
        private int _sliderValue;

        [ObservableProperty]
        private ObservableCollection<string> _sliderLabels = new();

        [ObservableProperty]
        private RegistrySettingStatus _status = RegistrySettingStatus.Unknown;

        [ObservableProperty]
        private object? _currentValue;

        /// <summary>
        /// Gets a value indicating whether this setting is command-based rather than registry-based.
        /// </summary>
        public bool IsCommandBasedSetting
        {
            get
            {
                // Check if this setting has PowerCfg commands in its CustomProperties
                return CustomProperties != null &&
                       CustomProperties.ContainsKey("PowerCfgSettings") &&
                       CustomProperties["PowerCfgSettings"] is List<Winhance.Core.Features.Optimize.Models.PowerCfgSetting> powerCfgSettings &&
                       powerCfgSettings.Count > 0;
            }
        }
        
        /// <summary>
        /// Gets a list of PowerCfg commands for display in tooltips.
        /// </summary>
        public ObservableCollection<string> PowerCfgCommands
        {
            get
            {
                var commands = new ObservableCollection<string>();
                
                if (IsCommandBasedSetting &&
                    CustomProperties.ContainsKey("PowerCfgSettings") &&
                    CustomProperties["PowerCfgSettings"] is List<Winhance.Core.Features.Optimize.Models.PowerCfgSetting> powerCfgSettings)
                {
                    foreach (var setting in powerCfgSettings)
                    {
                        commands.Add(setting.Command);
                    }
                }
                
                return commands;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the registry value is null.
        /// </summary>
        public bool IsRegistryValueNull
        {
            get
            {
                // Don't show warning for command-based settings
                if (IsCommandBasedSetting)
                {
                    return false;
                }
                
                // Don't show warning for settings with ActionType = Remove
                if (RegistrySetting != null && RegistrySetting.ActionType == RegistryActionType.Remove)
                {
                    Console.WriteLine($"DEBUG - IsRegistryValueNull for '{Name}': Returning false because ActionType is Remove");
                    return false;
                }
                
                // Also check linked registry settings for ActionType = Remove
                if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
                {
                    // If all linked settings have ActionType = Remove, don't show warning
                    bool allRemove = LinkedRegistrySettings.Settings.All(s => s.ActionType == RegistryActionType.Remove);
                    if (allRemove)
                    {
                        // For Remove actions, we don't want to show the warning if all values are null (which means keys don't exist)
                        bool allNull = LinkedRegistrySettingsWithValues.All(lv => lv.CurrentValue == null);
                        if (allNull)
                        {
                            Console.WriteLine($"DEBUG - IsRegistryValueNull for '{Name}': Returning false because all Remove settings have null values (keys don't exist)");
                            return false;
                        }
                        
                        // If any key exists when it shouldn't, we might want to show a warning
                        Console.WriteLine($"DEBUG - IsRegistryValueNull for '{Name}': Continuing because some Remove settings have non-null values (keys exist)");
                    }
                }
                
                // Add debug logging to help diagnose the issue
                Console.WriteLine($"DEBUG - IsRegistryValueNull for '{Name}': CurrentValue = {(CurrentValue == null ? "null" : CurrentValue.ToString())}");
                
                // Check if the registry value is null or a special value that indicates it doesn't exist
                if (CurrentValue == null)
                {
                    Console.WriteLine($"DEBUG - IsRegistryValueNull for '{Name}': Returning true because CurrentValue is null");
                    return true;
                }
                
                // Check if the registry setting exists
                if (RegistrySetting != null)
                {
                    // If we have a registry setting but no current value, it might be null
                    if (Status == RegistrySettingStatus.Unknown)
                    {
                        Console.WriteLine($"DEBUG - IsRegistryValueNull for '{Name}': Returning true because Status is Unknown");
                        return true;
                    }
                }
                
                Console.WriteLine($"DEBUG - IsRegistryValueNull for '{Name}': Returning false");
                return false;
            }
        }

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isApplying;

        /// <summary>
        /// Gets or sets the registry setting associated with this view model.
        /// </summary>
        public RegistrySetting? RegistrySetting { get; set; }

        /// <summary>
        /// Gets or sets the linked registry settings associated with this view model.
        /// </summary>
        public LinkedRegistrySettings LinkedRegistrySettings { get; set; } = new LinkedRegistrySettings();

        /// <summary>
        /// Gets or sets the linked registry settings with their current values for display in tooltips.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue> _linkedRegistrySettingsWithValues = new();

        /// <summary>
        /// Gets or sets the dependencies between settings.
        /// </summary>
        public List<SettingDependency> Dependencies { get; set; } = new List<SettingDependency>();

        /// <summary>
        /// Gets or sets custom properties for this setting.
        /// This can be used to store additional data specific to certain optimization types,
        /// such as PowerCfg settings.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the command to apply the setting.
        /// </summary>
        public ICommand ApplySettingCommand { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationSettingViewModel"/> class.
        /// </summary>
        public ApplicationSettingViewModel()
        {
            // Default constructor for design-time use
            ApplySettingCommand = new RelayCommand(async () => await ApplySetting());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationSettingViewModel"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dependencyManager">The dependency manager.</param>
        public ApplicationSettingViewModel(
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService,
            IDependencyManager? dependencyManager = null,
            Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService? powerPlanService = null)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _dialogService = dialogService; // Allow null for dialogService
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dependencyManager = dependencyManager; // Allow null for backward compatibility
            _powerPlanService = powerPlanService; // Allow null for backward compatibility
            
            // Initialize the ApplySettingCommand
            ApplySettingCommand = new RelayCommand(async () => await ApplySetting());

            // Set up property changed handlers for immediate application
            this.PropertyChanged += (s, e) => {
                if ((e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(SliderValue)) && !IsUpdatingFromCode)
                {
                    _logService?.Log(LogLevel.Info, $"Property {e.PropertyName} changed for {Name}, applying setting");

                    // Check dependencies when enabling a setting
                    if (e.PropertyName == nameof(IsSelected))
                    {
                        if (IsSelected)
                        {
                            // Check if this setting can be enabled based on its dependencies
                            var allSettings = GetAllSettings();
                            if (allSettings != null && _dependencyManager != null)
                            {
                                if (!_dependencyManager.CanEnableSetting(Id, allSettings))
                                {
                                    _logService?.Log(LogLevel.Info, $"Setting {Name} has unsatisfied dependencies, attempting to enable them");
                                    
                                    // Find the dependencies that need to be enabled
                                    var unsatisfiedDependencies = new List<ISettingItem>();
                                    foreach (var dependency in Dependencies)
                                    {
                                        if (dependency.DependencyType == SettingDependencyType.RequiresEnabled)
                                        {
                                            var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                                            if (requiredSetting != null && !requiredSetting.IsSelected)
                                            {
                                                unsatisfiedDependencies.Add(requiredSetting);
                                            }
                                        }
                                    }
                                    
                                    if (unsatisfiedDependencies.Count > 0)
                                    {
                                        // Automatically enable the dependencies without asking
                                        bool enableDependencies = true;
                                        
                                        // Log what we're doing
                                        var dependencyNames = string.Join(", ", unsatisfiedDependencies.Select(d => $"'{d.Name}'"));
                                        _logService?.Log(LogLevel.Info, $"'{Name}' requires {dependencyNames} to be enabled. Automatically enabling dependencies.");
                                        
                                        if (enableDependencies)
                                        {
                                            // Enable all dependencies
                                            foreach (var dependency in unsatisfiedDependencies)
                                            {
                                                _logService?.Log(LogLevel.Info, $"Automatically enabling dependency: {dependency.Name}");
                                                
                                                // Enable the dependency
                                                if (dependency is ApplicationSettingViewModel depViewModel)
                                                {
                                                    depViewModel.IsUpdatingFromCode = true;
                                                    try
                                                    {
                                                        depViewModel.IsSelected = true;
                                                    }
                                                    finally
                                                    {
                                                        depViewModel.IsUpdatingFromCode = false;
                                                    }
                                                    
                                                    // Apply the setting
                                                    depViewModel.ApplySettingCommand.Execute(null);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // User chose not to enable dependencies, so don't enable this setting
                                            IsUpdatingFromCode = true;
                                            try
                                            {
                                                IsSelected = false;
                                            }
                                            finally
                                            {
                                                IsUpdatingFromCode = false;
                                            }
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        // No dependencies found, show a generic message
                                        if (_dialogService != null)
                                        {
                                            _dialogService.ShowMessage(
                                                $"'{Name}' cannot be enabled because one of its dependencies is not satisfied.",
                                                "Setting Dependency");
                                        }
                                        
                                        // Prevent enabling this setting
                                        IsUpdatingFromCode = true;
                                        try
                                        {
                                            IsSelected = false;
                                        }
                                        finally
                                        {
                                            IsUpdatingFromCode = false;
                                        }
                                        return;
                                    }
                                }
                                
                                // Automatically enable any required settings
                                _dependencyManager.HandleSettingEnabled(Id, allSettings);
                            }
                        }
                        else
                        {
                            // Handle disabling a setting
                            var allSettings = GetAllSettings();
                            if (allSettings != null && _dependencyManager != null)
                            {
                                _dependencyManager.HandleSettingDisabled(Id, allSettings);
                            }
                        }
                    }

                    ApplySettingCommand.Execute(null);
                }
                else if ((e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(SliderValue)) && IsUpdatingFromCode)
                {
                    _logService?.Log(LogLevel.Info, $"Property {e.PropertyName} changed for {Name}, but not applying setting because IsUpdatingFromCode is true");
                }
            };
        }

        /// <summary>
        /// Called when the IsSelected property changes.
        /// </summary>
        /// <param name="value">The new value.</param>
        partial void OnIsSelectedChanged(bool value)
        {
            // If this is a grouped setting, update all child settings
            if (IsGroupedSetting && ChildSettings.Count > 0)
            {
                _isUpdatingFromCode = true;
                try
                {
                    foreach (var child in ChildSettings)
                    {
                        child.IsSelected = value;
                    }
                }
                finally
                {
                    _isUpdatingFromCode = false;
                }
            }
        }

        /// <summary>
        /// Applies the setting immediately.
        /// </summary>
        protected virtual async Task ApplySetting()
        {
            if (IsApplying || _registryService == null || _logService == null)
                return;

            try
            {
                IsApplying = true;

                // Check if this is a command-based setting (PowerCfg commands)
                if (IsCommandBasedSetting)
                {
                    _logService.Log(LogLevel.Info, $"Applying command-based setting: {Name}, IsSelected={IsSelected}");
                    
                    // Get the PowerPlanService from the application services
                    var powerPlanService = GetPowerPlanService();
                    if (powerPlanService == null)
                    {
                        _logService.Log(LogLevel.Error, $"Cannot apply command-based setting: {Name} - PowerPlanService not found");
                        Status = RegistrySettingStatus.Error;
                        StatusMessage = "Failed to apply setting: PowerPlanService not found";
                        return;
                    }
                    
                    // Get the PowerCfg settings from CustomProperties
                    var powerCfgSettings = CustomProperties["PowerCfgSettings"] as List<Winhance.Core.Features.Optimize.Models.PowerCfgSetting>;
                    if (powerCfgSettings == null || powerCfgSettings.Count == 0)
                    {
                        _logService.Log(LogLevel.Error, $"Cannot apply command-based setting: {Name} - PowerCfgSettings is null or empty");
                        Status = RegistrySettingStatus.Error;
                        StatusMessage = "Failed to apply setting: PowerCfgSettings is null or empty";
                        return;
                    }
                    
                    bool success;
                    
                    if (IsSelected)
                    {
                        // Apply the PowerCfg settings when enabling
                        _logService.Log(LogLevel.Info, $"Enabling command-based setting: {Name}");
                        success = await powerPlanService.ApplyPowerCfgSettingsAsync(powerCfgSettings);
                    }
                    else
                    {
                        // Apply the disabled values when disabling
                        _logService.Log(LogLevel.Info, $"Disabling command-based setting: {Name}");
                        
                        // Create a list of PowerCfgSetting objects with disabled values
                        var disabledSettings = new List<Winhance.Core.Features.Optimize.Models.PowerCfgSetting>();
                        foreach (var powerCfgSetting in powerCfgSettings)
                        {
                            if (!string.IsNullOrEmpty(powerCfgSetting.DisabledValue))
                            {
                                disabledSettings.Add(new Winhance.Core.Features.Optimize.Models.PowerCfgSetting
                                {
                                    Command = powerCfgSetting.DisabledValue.StartsWith("powercfg ")
                                        ? powerCfgSetting.DisabledValue
                                        : "powercfg " + powerCfgSetting.DisabledValue,
                                    Description = "Restore default: " + powerCfgSetting.Description,
                                    EnabledValue = powerCfgSetting.DisabledValue,
                                    DisabledValue = powerCfgSetting.EnabledValue
                                });
                            }
                        }
                        
                        // Apply the disabled settings
                        if (disabledSettings.Count > 0)
                        {
                            success = await powerPlanService.ApplyPowerCfgSettingsAsync(disabledSettings);
                        }
                        else
                        {
                            // If no disabled settings are defined, consider it a success
                            _logService.Log(LogLevel.Warning, $"No disabled values defined for command-based setting: {Name}");
                            success = true;
                        }
                    }
                    
                    if (success)
                    {
                        _logService.Log(LogLevel.Info, $"Successfully applied command-based setting: {Name}");
                        Status = IsSelected ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;
                        StatusMessage = IsSelected ? "Setting applied successfully" : "Setting disabled successfully";
                        
                        // Set a dummy current value to prevent warning icon
                        CurrentValue = IsSelected ? "Enabled" : "Disabled";
                    }
                    else
                    {
                        _logService.Log(LogLevel.Error, $"Failed to apply command-based setting: {Name}");
                        Status = RegistrySettingStatus.Error;
                        StatusMessage = "Failed to apply setting";
                    }
                    
                    return;
                }

                // Check if we have linked registry settings
                if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
                {
                    _logService.Log(LogLevel.Info, $"Applying linked registry settings for {Name}");
                    var linkedResult = await _registryService.ApplyLinkedSettingsAsync(LinkedRegistrySettings, IsSelected);

                    if (linkedResult)
                    {
                        _logService.Log(LogLevel.Info, $"Successfully applied linked settings for {Name}");
                        Status = RegistrySettingStatus.Applied;
                        StatusMessage = "Settings applied successfully";

                        // Update tooltip data for linked settings
                        if (_registryService != null && LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
                        {
                            // Update the tooltip data
                            LinkedRegistrySettingsWithValues.Clear();

                            // For linked settings, get fresh values from registry
                            foreach (var regSetting in LinkedRegistrySettings.Settings)
                            {
                                var regCurrentValue = _registryService.GetValue(
                                    RegistryExtensions.GetRegistryHiveString(regSetting.Hive) + "\\" + regSetting.SubKey,
                                    regSetting.Name);
                                LinkedRegistrySettingsWithValues.Add(new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(regSetting, regCurrentValue));
                            }

                            // Update the main current value display with the primary or first setting
                            var primarySetting = LinkedRegistrySettings.Settings.FirstOrDefault(s => s.IsPrimary) ??
                                               LinkedRegistrySettings.Settings.FirstOrDefault();
                            if (primarySetting != null)
                            {
                                var primaryValue = _registryService.GetValue(
                                    RegistryExtensions.GetRegistryHiveString(primarySetting.Hive) + "\\" + primarySetting.SubKey,
                                    primarySetting.Name);
                                CurrentValue = primaryValue;
                            }
                        }

                    }
                    else
                    {
                        _logService.Log(LogLevel.Error, $"Failed to apply linked settings for {Name}");
                        Status = RegistrySettingStatus.Error;
                        StatusMessage = "Failed to apply settings";
                    }
                    return;
                }

                // Fall back to single registry setting if no linked settings
                if (RegistrySetting == null)
                {
                    _logService.Log(LogLevel.Error, $"Cannot apply setting: {Name} - Registry setting is null");
                    return;
                }

                string valueToSet = string.Empty;
                object valueObject;

                // Determine value to set based on control type
                switch (ControlType)
                {
                    case ControlType.BinaryToggle:
                        if (IsSelected)
                        {
                            // When toggle is ON, use EnabledValue if available, otherwise fall back to RecommendedValue
                            valueObject = RegistrySetting.EnabledValue ?? RegistrySetting.RecommendedValue;
                        }
                        else
                        {
                            // When toggle is OFF, use DisabledValue if available, otherwise fall back to DefaultValue
                            valueObject = RegistrySetting.DisabledValue ?? RegistrySetting.DefaultValue;
                        }
                        break;

                    case ControlType.ThreeStateSlider:
                        // Map slider value to appropriate setting value
                        valueObject = SliderValue;
                        break;

                    case ControlType.Custom:
                    default:
                        // Custom handling would go here
                        valueObject = IsSelected ?
                            (RegistrySetting.EnabledValue ?? RegistrySetting.RecommendedValue) :
                            (RegistrySetting.DisabledValue ?? RegistrySetting.DefaultValue);
                        break;
                }

                // Check if this registry setting requires special handling
                if (RegistrySetting.RequiresSpecialHandling() && 
                    RegistrySetting.ApplySpecialHandling(_registryService, IsSelected))
                {
                    // Special handling was applied successfully
                    _logService.Log(LogLevel.Info, $"Special handling applied for {RegistrySetting.Name}");
                    Status = RegistrySettingStatus.Applied;
                    StatusMessage = "Setting applied successfully";
                    
                    // Update current value for tooltip display
                    if (_registryService != null)
                    {
                        // Get the current value after special handling
                        var currentValue = _registryService.GetValue(
                            RegistryExtensions.GetRegistryHiveString(RegistrySetting.Hive) + "\\" + RegistrySetting.SubKey,
                            RegistrySetting.Name);
                        
                        CurrentValue = currentValue;
                        
                        // Update the tooltip data
                        LinkedRegistrySettingsWithValues.Clear();
                        LinkedRegistrySettingsWithValues.Add(new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(RegistrySetting, currentValue));
                    }
                    
                    return;
                }
                else
                {
                    // Apply the registry change normally
                    var result = _registryService.SetValue(
                        RegistryExtensions.GetRegistryHiveString(RegistrySetting.Hive) + "\\" + RegistrySetting.SubKey,
                        RegistrySetting.Name,
                        valueObject,
                        RegistrySetting.ValueType);

                    if (result)
                    {
                        _logService.Log(LogLevel.Info, $"Setting applied: {Name}");
                        Status = RegistrySettingStatus.Applied;
                        StatusMessage = "Setting applied successfully";

                        // Update current value for tooltip display
                        if (_registryService != null)
                        {
                            // Update the current value
                            CurrentValue = valueObject;

                            // Update the tooltip data
                            LinkedRegistrySettingsWithValues.Clear();
                            if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
                            {
                                // For linked settings
                                foreach (var regSetting in LinkedRegistrySettings.Settings)
                                {
                                    // For the setting that was just changed, use the new value
                                    if (regSetting.SubKey == RegistrySetting.SubKey &&
                                        regSetting.Name == RegistrySetting.Name &&
                                        regSetting.Hive == RegistrySetting.Hive)
                                    {
                                        LinkedRegistrySettingsWithValues.Add(new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(regSetting, valueObject));
                                    }
                                    else
                                    {
                                        // For other linked settings, get the current value from registry
                                        var regCurrentValue = _registryService.GetValue(
                                            RegistryExtensions.GetRegistryHiveString(regSetting.Hive) + "\\" + regSetting.SubKey,
                                            regSetting.Name);
                                        LinkedRegistrySettingsWithValues.Add(new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(regSetting, regCurrentValue));
                                    }
                                }
                            }
                            else if (RegistrySetting != null)
                            {
                                // For single setting
                                LinkedRegistrySettingsWithValues.Add(new Winhance.WPF.Features.Common.Models.LinkedRegistrySettingWithValue(RegistrySetting, valueObject));
                            }
                        }

                        // Check if restart is required
                        bool requiresRestart = Name.Contains("restart", StringComparison.OrdinalIgnoreCase);
                        if (requiresRestart && _dialogService != null)
                        {
                            _dialogService.ShowMessage(
                                "This change requires a system restart to take effect.",
                                "Restart Required");
                        }
                    }
                    else
                    {
                        _logService.Log(LogLevel.Error, $"Failed to apply setting: {Name}");
                        Status = RegistrySettingStatus.Error;
                        StatusMessage = "Failed to apply setting";

                        if (_dialogService != null)
                        {
                            _dialogService.ShowMessage(
                                $"Failed to apply {Name}. This may require administrator privileges.",
                                "Error");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying setting: {ex.Message}");
                Status = RegistrySettingStatus.Error;
                StatusMessage = $"Error: {ex.Message}";

                if (_dialogService != null)
                {
                    _dialogService.ShowMessage(
                        $"An error occurred: {ex.Message}",
                        "Error");
                }
            }
            finally
            {
                IsApplying = false;
            }
        }

        /// <summary>
        /// Gets all settings from all view models in the application.
        /// </summary>
        /// <returns>A list of all settings, or null if none found.</returns>
        protected virtual List<ISettingItem>? GetAllSettings()
        {
            try
            {
                _logService?.Log(LogLevel.Info, $"Getting all settings for dependency check");
                
                // Check if we have a dependency manager with a settings registry
                if (_dependencyManager is IDependencyManager dependencyManager && 
                    dependencyManager.GetType().GetProperty("SettingsRegistry")?.GetValue(dependencyManager) is ISettingsRegistry settingsRegistry)
                {
                    var settings = settingsRegistry.GetAllSettings();
                    if (settings.Count > 0)
                    {
                        _logService?.Log(LogLevel.Info, $"Found {settings.Count} settings in settings registry via dependency manager");
                        return settings;
                    }
                }
                
                // If no settings registry is available or it's empty, fall back to the original implementation
                var result = new List<ISettingItem>();
                var app = System.Windows.Application.Current;
                if (app == null) return null;

                // Try to find all settings view models
                foreach (System.Windows.Window window in app.Windows)
                {
                    if (window.DataContext != null)
                    {
                        // Look for view models that have a Settings property
                        var settingsProperties = window.DataContext.GetType().GetProperties()
                            .Where(p => p.Name == "Settings" || p.Name.EndsWith("Settings"));

                        foreach (var prop in settingsProperties)
                        {
                            var value = prop.GetValue(window.DataContext);
                            if (value is IEnumerable<ISettingItem> settings)
                            {
                                result.AddRange(settings);
                                _logService?.Log(LogLevel.Info, $"Found {settings.Count()} settings in {prop.Name}");
                            }
                        }

                        // Also look for view models that might contain other view models with settings
                        var viewModelProperties = window.DataContext.GetType().GetProperties()
                            .Where(p => p.Name.EndsWith("ViewModel") && p.Name != "DataContext");

                        foreach (var prop in viewModelProperties)
                        {
                            var viewModel = prop.GetValue(window.DataContext);
                            if (viewModel != null)
                            {
                                var nestedSettingsProps = viewModel.GetType().GetProperties()
                                    .Where(p => p.Name == "Settings" || p.Name.EndsWith("Settings"));

                                foreach (var nestedProp in nestedSettingsProps)
                                {
                                    var value = nestedProp.GetValue(viewModel);
                                    if (value is IEnumerable<ISettingItem> settings)
                                    {
                                        result.AddRange(settings);
                                        _logService?.Log(LogLevel.Info, $"Found {settings.Count()} settings in {prop.Name}.{nestedProp.Name}");
                                    }
                                }
                            }
                        }
                    }
                }

                // If we found settings, return them
                if (result.Count > 0)
                {
                    _logService?.Log(LogLevel.Info, $"Found a total of {result.Count} settings through property scanning");
                    
                    // Log the settings we found
                    foreach (var setting in result)
                    {
                        _logService?.Log(LogLevel.Info, $"Found setting: {setting.Id} - {setting.Name}");
                    }
                    
                    // If we found a settings registry earlier, register these settings for future use
                    if (_dependencyManager is IDependencyManager dependencyManager2 && 
                        dependencyManager2.GetType().GetProperty("SettingsRegistry")?.GetValue(dependencyManager2) is ISettingsRegistry settingsRegistry2)
                    {
                        foreach (var setting in result)
                        {
                            settingsRegistry2.RegisterSetting(setting);
                        }
                        _logService?.Log(LogLevel.Info, $"Registered {result.Count} settings in settings registry for future use");
                    }
                    
                    return result;
                }

                _logService?.Log(LogLevel.Warning, "Could not find any settings for dependency check");
                return null;
            }
            catch (Exception ex)
            {
                _logService?.Log(LogLevel.Error, $"Error getting all settings: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets the PowerPlanService from the application services.
        /// </summary>
        /// <returns>The PowerPlanService, or null if not found.</returns>
        protected Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService? GetPowerPlanService()
        {
            // If we already have a PowerPlanService, return it
            if (_powerPlanService != null)
            {
                return _powerPlanService;
            }
            
            try
            {
                // Try to get the PowerPlanService from the application services
                var app = System.Windows.Application.Current;
                if (app == null) return null;
                
                // Check if the application has a GetService method
                var getServiceMethod = app.GetType().GetMethod("GetService", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (getServiceMethod != null)
                {
                    // Call the GetService method to get the PowerPlanService
                    var powerPlanService = getServiceMethod.Invoke(app, new object[] { typeof(Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService) });
                    return powerPlanService as Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService;
                }
                
                // If the application doesn't have a GetService method, try to find the PowerPlanService in the application resources
                foreach (System.Windows.Window window in app.Windows)
                {
                    if (window.DataContext != null)
                    {
                        // Look for view models that might have a PowerPlanService property
                        var powerPlanServiceProperty = window.DataContext.GetType().GetProperty("PowerPlanService");
                        if (powerPlanServiceProperty != null)
                        {
                            var powerPlanService = powerPlanServiceProperty.GetValue(window.DataContext);
                            if (powerPlanService is Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService)
                            {
                                return powerPlanService as Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService;
                            }
                        }
                        
                        // Look for view models that might contain other view models with a PowerPlanService property
                        var viewModelProperties = window.DataContext.GetType().GetProperties()
                            .Where(p => p.Name.EndsWith("ViewModel") && p.Name != "DataContext");
                        
                        foreach (var prop in viewModelProperties)
                        {
                            var viewModel = prop.GetValue(window.DataContext);
                            if (viewModel != null)
                            {
                                var nestedPowerPlanServiceProperty = viewModel.GetType().GetProperty("PowerPlanService");
                                if (nestedPowerPlanServiceProperty != null)
                                {
                                    var powerPlanService = nestedPowerPlanServiceProperty.GetValue(viewModel);
                                    if (powerPlanService is Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService)
                                    {
                                        return powerPlanService as Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService;
                                    }
                                }
                                
                                // Check if this view model has a _powerPlanService field
                                var powerPlanServiceField = viewModel.GetType().GetField("_powerPlanService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (powerPlanServiceField != null)
                                {
                                    var powerPlanService = powerPlanServiceField.GetValue(viewModel);
                                    if (powerPlanService is Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService)
                                    {
                                        return powerPlanService as Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService;
                                    }
                                }
                            }
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logService?.Log(LogLevel.Error, $"Error getting PowerPlanService: {ex.Message}");
                return null;
            }
        }
    }
}
