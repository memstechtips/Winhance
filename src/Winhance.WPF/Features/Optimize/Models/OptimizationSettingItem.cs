using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Microsoft.Win32;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Optimize.Models
{
    /// <summary>
    /// View model for an optimization setting item.
    /// </summary>
    public partial class OptimizationSettingItem : ObservableObject, ISettingItem, ISearchable
    {
        private readonly IRegistryService? _registryService;
        private readonly IDialogService? _dialogService;
        private readonly ILogService? _logService;
        private bool _isUpdatingFromCode;

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

        [ObservableProperty]
        private bool _isGroupHeader;

        [ObservableProperty]
        private string _groupName = string.Empty;

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
        /// Gets a value indicating whether the registry value is null.
        /// </summary>
        public bool IsRegistryValueNull
        {
            get
            {
                // Only return true if CurrentValue is actually null
                // This ensures empty strings, 0, false, etc. are not treated as null
                return CurrentValue == null;
            }
        }

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isApplying;

        /// <summary>
        /// Gets or sets a value indicating whether the setting is visible.
        /// </summary>
        [ObservableProperty]
        private bool _isVisible = true;

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
        private ObservableCollection<LinkedRegistrySettingWithValue> _linkedRegistrySettingsWithValues = new();

        /// <summary>
        /// Gets or sets the dependencies for this setting.
        /// </summary>
        public List<SettingDependency> Dependencies { get; set; } = new List<SettingDependency>();

        /// <summary>
        /// Gets or sets the command to apply the setting.
        /// </summary>
        ICommand ISettingItem.ApplySettingCommand
        {
            get => ApplySettingCommand;
        }

        /// <summary>
        /// Gets or sets the command to apply the setting.
        /// </summary>
        public ICommand ApplySettingCommand { get; set; }
        
        /// <summary>
        /// Determines if the object matches the given search term.
        /// </summary>
        /// <param name="searchTerm">The search term to match against.</param>
        /// <returns>True if the object matches the search term, false otherwise.</returns>
        public bool MatchesSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return true;

            searchTerm = searchTerm.ToLowerInvariant();

            // Add detailed logging to understand matching behavior
            // Use StringComparison.OrdinalIgnoreCase for case-insensitive comparison
            bool matchesName = Name != null && Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            bool matchesDescription = Description != null && Description.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            bool matchesGroupName = GroupName != null && GroupName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
            
            // Log the matching details for all settings when searching
            // This will help diagnose search issues with any term
            Console.WriteLine($"DEBUG - MatchesSearch for '{Name}' (ID: {Id}, Group: '{GroupName}') with term '{searchTerm}':");
            Console.WriteLine($"  - Matches Name: {matchesName} (Name: '{Name}')");
            Console.WriteLine($"  - Matches Description: {matchesDescription} (Description: '{Description}')");
            Console.WriteLine($"  - Matches GroupName: {matchesGroupName} (GroupName: '{GroupName}')");
            Console.WriteLine($"  - Result: {matchesName || matchesDescription || matchesGroupName}");

            // Check if the search term matches the name, description, or group name
            bool result = matchesName || matchesDescription || matchesGroupName;
            
            // Log all successful matches
            if (result)
            {
                Console.WriteLine($"MATCH FOUND: '{Name}' (ID: {Id}, Group: '{GroupName}') matches search term '{searchTerm}'");
            }
            
            return result;
        }

        /// <summary>
        /// Gets the searchable properties of the object.
        /// </summary>
        /// <returns>An array of property names that should be searched.</returns>
        public string[] GetSearchableProperties()
        {
            return new[] { nameof(Name), nameof(Description), nameof(GroupName) };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizationSettingItem"/> class.
        /// </summary>
        public OptimizationSettingItem()
        {
            // Default constructor for design-time use
            ApplySettingCommand = new RelayCommand(async () => await ApplySetting());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizationSettingItem"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        public OptimizationSettingItem(
            IRegistryService registryService,
            IDialogService? dialogService,
            ILogService logService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _dialogService = dialogService; // Allow null for dialogService
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Initialize the ApplySettingCommand
            ApplySettingCommand = new RelayCommand(async () => await ApplySetting());

            // Set up property changed handlers for immediate application
            this.PropertyChanged += (s, e) => {
                if ((e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(SliderValue)) && !IsUpdatingFromCode)
                {
                    _logService?.Log(LogLevel.Info, $"Property {e.PropertyName} changed for {Name}, applying setting");
                    ApplySettingCommand.Execute(null);
                }
                else if ((e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(SliderValue)) && IsUpdatingFromCode)
                {
                    _logService?.Log(LogLevel.Info, $"Property {e.PropertyName} changed for {Name}, but not applying setting because IsUpdatingFromCode is true");
                }
            };

            // Initialize FullName
            UpdateFullName();
        }

        /// <summary>
        /// Applies the setting immediately.
        /// </summary>
        private async Task ApplySetting()
        {
            if (IsApplying || _registryService == null || _logService == null)
                return;

            try
            {
                IsApplying = true;

                // Check if we have linked registry settings
                if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
                {
                    _logService.Log(LogLevel.Info, $"Applying linked setting: {Name} with {LinkedRegistrySettings.Settings.Count} registry entries");
                    
                    // Apply all linked settings using the ApplyLinkedSettingsAsync method
                    bool result = await _registryService.ApplyLinkedSettingsAsync(LinkedRegistrySettings, IsSelected);
                    
                    if (result)
                    {
                        _logService.Log(LogLevel.Info, $"Successfully applied linked settings for {Name}");
                        Status = RegistrySettingStatus.Applied;
                        StatusMessage = "Setting applied successfully";
                        
                        // Update the tooltip data
                        LinkedRegistrySettingsWithValues.Clear();
                        foreach (var regSetting in LinkedRegistrySettings.Settings)
                        {
                            var regCurrentValue = _registryService.GetValue(
                                GetRegistryHiveString(regSetting.Hive) + "\\" + regSetting.SubKey,
                                regSetting.Name);
                            LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(regSetting, regCurrentValue));
                        }
                        
                        // Check if restart is required
                        bool requiresRestart = FullName.Contains("restart", StringComparison.OrdinalIgnoreCase);
                        if (requiresRestart && _dialogService != null)
                        {
                            _dialogService.ShowMessage(
                                "This change requires a system restart to take effect.",
                                "Restart Required");
                        }
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to apply linked settings for {Name}");
                        Status = RegistrySettingStatus.Error;
                        StatusMessage = "Failed to apply setting";
                        
                        if (_dialogService != null)
                        {
                            _dialogService.ShowMessage(
                                $"Failed to apply {FullName}. This may require administrator privileges.",
                                "Error");
                        }
                    }
                    
                    return;
                }
                
                // Fall back to single registry setting if no linked settings
                if (RegistrySetting == null)
                {
                    _logService.Log(LogLevel.Warning, $"Cannot apply setting: {FullName} - Registry setting is null");
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
                        _logService.Log(LogLevel.Debug, $"ThreeStateSlider: Using SliderValue {SliderValue} for {Name}");
                        break;
                        
                    case ControlType.ComboBox:
                        // For ComboBox, use SliderValue as the index
                        valueObject = SliderValue;
                        _logService.Log(LogLevel.Debug, $"ComboBox: Using SliderValue {SliderValue} for {Name}");
                        break;

                    case ControlType.Custom:
                    default:
                        // Custom handling would go here
                        valueObject = IsSelected ?
                            (RegistrySetting.EnabledValue ?? RegistrySetting.RecommendedValue) :
                            (RegistrySetting.DisabledValue ?? RegistrySetting.DefaultValue);
                        break;
                }

                // Apply the registry change normally
                var singleResult = _registryService.SetValue(
                    GetRegistryHiveString(RegistrySetting.Hive) + "\\" + RegistrySetting.SubKey,
                    RegistrySetting.Name,
                    valueObject,
                    RegistrySetting.ValueType);

                if (singleResult)
                {
                    _logService.Log(LogLevel.Info, $"Setting applied: {FullName}");
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
                                    LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(regSetting, valueObject));
                                }
                                else
                                {
                                    // For other linked settings, get the current value from registry
                                    var regCurrentValue = _registryService.GetValue(
                                        GetRegistryHiveString(regSetting.Hive) + "\\" + regSetting.SubKey,
                                        regSetting.Name);
                                    LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(regSetting, regCurrentValue));
                                }
                            }
                        }
                        else if (RegistrySetting != null)
                        {
                            // For single setting
                            LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(RegistrySetting, valueObject));
                        }
                    }

                    // Check if restart is required
                    bool requiresRestart = FullName.Contains("restart", StringComparison.OrdinalIgnoreCase);
                    if (requiresRestart && _dialogService != null)
                    {
                        _dialogService.ShowMessage(
                            "This change requires a system restart to take effect.",
                            "Restart Required");
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Warning, $"Failed to apply setting: {FullName}");
                    Status = RegistrySettingStatus.Error;
                    StatusMessage = "Failed to apply setting";

                    if (_dialogService != null)
                    {
                        _dialogService.ShowMessage(
                            $"Failed to apply {FullName}. This may require administrator privileges.",
                            "Error");
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
                _ => throw new ArgumentException($"Unsupported registry hive: {hive}")
            };
        }
    }
}