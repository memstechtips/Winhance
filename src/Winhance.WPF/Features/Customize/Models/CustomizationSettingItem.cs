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

namespace Winhance.WPF.Features.Customize.Models
{
    /// <summary>
    /// View model for a customization setting item.
    /// </summary>
    public partial class CustomizationSettingItem : ObservableObject, ISettingItem, ISearchable
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

        /// <summary>
        /// Gets or sets the selected value for ComboBox controls.
        /// </summary>
        [ObservableProperty]
        private string _selectedValue = string.Empty;

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
                // Don't show warning for settings with ActionType = Remove
                if (RegistrySetting != null && RegistrySetting.ActionType == RegistryActionType.Remove)
                {
                    return false;
                }
                
                // Also check linked registry settings for ActionType = Remove
                if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
                {
                    // If all linked settings have ActionType = Remove, don't show warning
                    bool allRemove = LinkedRegistrySettings.Settings.All(s => s.ActionType == RegistryActionType.Remove);
                    if (allRemove)
                    {
                        return false;
                    }
                }
                
                // Original check
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
        /// Gets a value indicating whether this is a command-based setting.
        /// </summary>
        public bool IsCommandBasedSetting => false;

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

        // Check if the search term matches the name or description
        return Name.ToLowerInvariant().Contains(searchTerm) ||
               Description.ToLowerInvariant().Contains(searchTerm) ||
               GroupName.ToLowerInvariant().Contains(searchTerm);
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
        /// Initializes a new instance of the <see cref="CustomizationSettingItem"/> class.
        /// </summary>
        public CustomizationSettingItem()
        {
            // Default constructor for design-time use
            ApplySettingCommand = new RelayCommand(async () => await ApplySetting());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizationSettingItem"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="logService">The log service.</param>
        public CustomizationSettingItem(
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
                // Check if the property that changed should trigger an apply setting
                bool shouldApply = e.PropertyName == nameof(IsSelected) ||
                                   e.PropertyName == nameof(SliderValue) ||
                                   e.PropertyName == nameof(SliderLabels) ||
                                   e.PropertyName == nameof(SelectedValue);
                
                if (shouldApply && !IsUpdatingFromCode)
                {
                    _logService?.Log(LogLevel.Info, $"Property {e.PropertyName} changed for {Name}, applying setting");
                    ApplySettingCommand.Execute(null);
                }
                else if (shouldApply && IsUpdatingFromCode)
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
                    
                    // Special handling for News and Interests (Widgets) toggle
                    bool result;
                    if (Name == "News and Interests (Widgets)")
                    {
                        _logService.Log(LogLevel.Info, $"Using special handling for News and Interests (Widgets) toggle");
                        result = await Winhance.Core.Features.Customize.Models.TaskbarCustomizations.ApplyNewsAndInterestsSettingsAsync(
                            _registryService, _logService, LinkedRegistrySettings, IsSelected);
                    }
                    else
                    {
                        // Apply all linked settings using the standard method
                        result = await _registryService.ApplyLinkedSettingsAsync(LinkedRegistrySettings, IsSelected);
                    }
                    
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
                        _logService.Log(LogLevel.Error, $"Failed to apply linked settings for {Name}");
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
                    _logService.Log(LogLevel.Error, $"Cannot apply setting: {FullName} - Registry setting is null");
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
                        
                    case ControlType.ComboBox:
                        // For ComboBox, first check if we have a direct SelectedValue
                        if (!string.IsNullOrEmpty(SelectedValue))
                        {
                            _logService.Log(LogLevel.Info, $"ComboBox direct selected value: {SelectedValue}");
                            
                            // Map the selected value to a registry value
                            // For Dark Mode/Light Mode, we map "Dark Mode" to 1 and "Light Mode" to 0
                            if (SelectedValue == "Dark Mode")
                            {
                                valueObject = 1;
                            }
                            else if (SelectedValue == "Light Mode")
                            {
                                valueObject = 0;
                            }
                            else
                            {
                                // Try to find the index of the selected value in the SliderLabels collection
                                int index = SliderLabels.IndexOf(SelectedValue);
                                if (index >= 0)
                                {
                                    valueObject = index;
                                }
                                else
                                {
                                    // Default to the SliderValue if we can't map the selected value
                                    valueObject = SliderValue;
                                }
                            }
                        }
                        // Fall back to using SliderValue as an index into the SliderLabels collection
                        else if (SliderLabels.Count > 0 && SliderValue >= 0 && SliderValue < SliderLabels.Count)
                        {
                            string selectedValue = SliderLabels[SliderValue];
                            _logService.Log(LogLevel.Info, $"ComboBox selected value from slider: {selectedValue}");
                            
                            // Map the selected value to a registry value
                            // For Dark Mode/Light Mode, we map "Dark Mode" to 1 and "Light Mode" to 0
                            if (selectedValue == "Dark Mode")
                            {
                                valueObject = 1;
                            }
                            else if (selectedValue == "Light Mode")
                            {
                                valueObject = 0;
                            }
                            else
                            {
                                // Default to the SliderValue if we can't map the selected value
                                valueObject = SliderValue;
                            }
                        }
                        else
                        {
                            // If we don't have a valid selection, use the SliderValue directly
                            valueObject = SliderValue;
                        }
                        break;

                    case ControlType.Custom:
                    default:
                        // Custom handling would go here
                        valueObject = IsSelected ?
                            (RegistrySetting.EnabledValue ?? RegistrySetting.RecommendedValue) :
                            (RegistrySetting.DisabledValue ?? RegistrySetting.DefaultValue);
                        break;
                }

                // Special case for HttpAcceptLanguageOptOut
                bool isHttpAcceptLanguageOptOut = RegistrySetting.SubKey == "Control Panel\\International\\User Profile" &&
                                                RegistrySetting.Name == "HttpAcceptLanguageOptOut";

                if (isHttpAcceptLanguageOptOut && IsSelected && RegistrySetting.EnabledValue != null &&
                    ((RegistrySetting.EnabledValue is int intValue && intValue == 0) ||
                     (RegistrySetting.EnabledValue is string strValue && strValue == "0")))
                {
                    // When enabling language list access, Windows deletes the key entirely
                    _logService.Log(LogLevel.Info, $"Special case: Deleting HttpAcceptLanguageOptOut key to enable language list access");
                    var result = _registryService.DeleteValue(
                        GetRegistryHiveString(RegistrySetting.Hive) + "\\" + RegistrySetting.SubKey,
                        RegistrySetting.Name);
                }
                // Special handling for ActionType.Remove settings
                else if (RegistrySetting.ActionType == RegistryActionType.Remove)
                {
                    bool result;
                    string fullPath = GetRegistryHiveString(RegistrySetting.Hive) + "\\" + RegistrySetting.SubKey;
                    
                    if (IsSelected)
                    {
                        // When enabling a setting with ActionType.Remove, we need to create the key
                        _logService.Log(LogLevel.Info, $"Creating registry key for ActionType.Remove setting: {fullPath}");
                        
                        // If the Name is a GUID, it's likely a subkey that needs to be created
                        if (Guid.TryParse(RegistrySetting.Name.Trim('{', '}'), out _))
                        {
                            // Create a subkey with the GUID name
                            string fullKeyPath = $"{fullPath}\\{RegistrySetting.Name}";
                            _logService.Log(LogLevel.Info, $"Creating GUID subkey: {fullKeyPath}");
                            result = _registryService.CreateKey(fullKeyPath);
                        }
                        else
                        {
                            // Just create the main key
                            result = _registryService.CreateKey(fullPath);
                        }
                    }
                    else
                    {
                        // When disabling, delete the key or value
                        if (Guid.TryParse(RegistrySetting.Name.Trim('{', '}'), out _))
                        {
                            // Delete the GUID subkey
                            string fullKeyPath = $"{fullPath}\\{RegistrySetting.Name}";
                            _logService.Log(LogLevel.Info, $"Deleting GUID subkey: {fullKeyPath}");
                            result = _registryService.DeleteKey(fullKeyPath);
                        }
                        else
                        {
                            // Delete the value
                            _logService.Log(LogLevel.Info, $"Deleting registry value: {fullPath}\\{RegistrySetting.Name}");
                            result = _registryService.DeleteValue(fullPath, RegistrySetting.Name);
                        }
                    }
                    
                    // Process the result
                    if (result)
                    {
                        _logService.Log(LogLevel.Info, $"Setting applied: {FullName}");
                        Status = RegistrySettingStatus.Applied;
                        StatusMessage = "Setting applied successfully";

                        // Update current value for tooltip display
                        if (_registryService != null)
                        {
                            // Update the current value
                            CurrentValue = IsSelected ? "Key exists" : "Key removed";

                            // Update the tooltip data
                            LinkedRegistrySettingsWithValues.Clear();
                            if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
                            {
                                // For linked settings
                                foreach (var regSetting in LinkedRegistrySettings.Settings)
                                {
                                    LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(
                                        regSetting,
                                        IsSelected ? "Key exists" : "Key removed"));
                                }
                            }
                            else if (RegistrySetting != null)
                            {
                                // For single setting
                                LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(
                                    RegistrySetting,
                                    IsSelected ? "Key exists" : "Key removed"));
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
                        _logService.Log(LogLevel.Error, $"Failed to apply setting: {FullName}");
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
                else
                {
                    // Apply the registry change normally
                    bool result = _registryService.SetValue(
                        GetRegistryHiveString(RegistrySetting.Hive) + "\\" + RegistrySetting.SubKey,
                        RegistrySetting.Name,
                        valueObject,
                        RegistrySetting.ValueType);

                    if (result)
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
                        _logService.Log(LogLevel.Error, $"Failed to apply setting: {FullName}");
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
