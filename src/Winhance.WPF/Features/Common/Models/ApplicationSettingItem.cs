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

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Base class for application setting items used in both Optimization and Customization features.
    /// </summary>
    public partial class ApplicationSettingItem : ObservableObject, ISettingItem, ISearchable
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

        partial void OnIsSelectedChanged(bool value)
        {
            // Skip if we're updating from code
            if (IsUpdatingFromCode)
            {
                return;
            }

            // Store the current selection state to restore after applying
            bool currentSelection = value;

            // Apply the setting when IsSelected changes
            ApplySetting();
            
            // Ensure the toggle stays in the state the user selected
            if (IsSelected != currentSelection)
            {
                IsUpdatingFromCode = true;
                try
                {
                    IsSelected = currentSelection;
                }
                finally
                {
                    IsUpdatingFromCode = false;
                }
            }
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

        [ObservableProperty]
        private object? _selectedValue;

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
                    LinkedRegistrySettingsWithValues.Clear();
                    foreach (var setting in value.Settings)
                    {
                        LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(setting, null));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the linked registry settings with values.
        /// </summary>
        public ObservableCollection<LinkedRegistrySettingWithValue> LinkedRegistrySettingsWithValues { get; set; } = new();

        /// <summary>
        /// Gets or sets the dependencies between settings.
        /// </summary>
        public List<SettingDependency> Dependencies { get; set; } = new List<SettingDependency>();

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
        /// Gets or sets a value indicating whether this is a grouped setting that contains child settings.
        /// </summary>
        public bool IsGroupedSetting { get; set; }

        /// <summary>
        /// Gets or sets the child settings for a grouped setting.
        /// </summary>
        public ObservableCollection<ApplicationSettingItem> ChildSettings { get; set; } = new ObservableCollection<ApplicationSettingItem>();

        /// <summary>
        /// Gets or sets a dictionary of custom properties.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the collection of actions associated with this setting.
        /// </summary>
        public List<Winhance.Core.Features.Common.Models.ApplicationAction> Actions { get; } = new List<Winhance.Core.Features.Common.Models.ApplicationAction>();

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
        public ApplicationSettingItem(IRegistryService? registryService, IDialogService? dialogService, ILogService? logService)
            : this()
        {
            _registryService = registryService;
            _dialogService = dialogService;
            _logService = logService;
        }

        /// <summary>
        /// Applies the setting.
        /// </summary>
        public void ApplySetting()
        {
            if (_registryService == null)
            {
                return;
            }

            // Skip if we're updating from code
            if (IsUpdatingFromCode)
            {
                return;
            }

            // Apply the setting
            if (RegistrySetting != null)
            {
                try
                {
                    // Get the registry hive string
                    string hiveString = GetRegistryHiveString(RegistrySetting.Hive);

                    // Get the appropriate value based on the toggle state
                    object valueToApply = IsSelected 
                        ? (RegistrySetting.EnabledValue ?? RegistrySetting.RecommendedValue) 
                        : (RegistrySetting.DisabledValue ?? RegistrySetting.DefaultValue);

                    // Apply the setting
                    _registryService.SetValue(
                        $"{hiveString}\\{RegistrySetting.SubKey}",
                        RegistrySetting.Name,
                        valueToApply,
                        RegistrySetting.ValueType);

                    // Update the current value and linked registry settings with values
                    CurrentValue = valueToApply;
                    
                    // Update the linked registry settings with values collection
                    LinkedRegistrySettingsWithValues.Clear();
                    LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(RegistrySetting, CurrentValue));

                    // Update status without changing IsSelected
                    Status = IsSelected ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;
                    StatusMessage = Status == RegistrySettingStatus.Applied ? "Applied" : "Not Applied";

                    // Log the action
                    _logService?.Log(LogLevel.Info, $"Applied setting {Name}: {(IsSelected ? "Enabled" : "Disabled")}");
                }
                catch (Exception ex)
                {
                    _logService?.Log(LogLevel.Error, $"Error applying setting {Name}: {ex.Message}");
                }
            }
            else if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
            {
                try
                {
                    // Clear the existing values
                    LinkedRegistrySettingsWithValues.Clear();
                    
                    // Apply all linked settings
                    foreach (var setting in LinkedRegistrySettings.Settings)
                    {
                        // Get the registry hive string
                        string hiveString = GetRegistryHiveString(setting.Hive);

                        // Get the appropriate value based on the toggle state
                        object valueToApply = IsSelected 
                            ? (setting.EnabledValue ?? setting.RecommendedValue) 
                            : (setting.DisabledValue ?? setting.DefaultValue);
                        
                        // Apply the setting
                        _registryService.SetValue(
                            $"{hiveString}\\{setting.SubKey}",
                            setting.Name,
                            valueToApply,
                            setting.ValueType);
                            
                        // Add to the linked registry settings with values collection
                        LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(setting, valueToApply));
                    }

                    // Update status without changing IsSelected
                    Status = IsSelected ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;
                    StatusMessage = Status == RegistrySettingStatus.Applied ? "Applied" : "Not Applied";

                    // Log the action
                    _logService?.Log(LogLevel.Info, $"Applied linked settings for {Name}: {(IsSelected ? "Enabled" : "Disabled")}");
                }
                catch (Exception ex)
                {
                    _logService?.Log(LogLevel.Error, $"Error applying linked settings for {Name}: {ex.Message}");
                }
            }

            // Don't call RefreshStatus() here to avoid triggering additional registry operations
        }

        /// <summary>
        /// Restores the setting to its default value.
        /// </summary>
        public void RestoreDefault()
        {
            if (_registryService == null)
            {
                return;
            }

            // Skip if we're updating from code
            if (IsUpdatingFromCode)
            {
                return;
            }

            // Restore the setting to its default value
            if (RegistrySetting != null)
            {
                // Get the registry hive string
                string hiveString = GetRegistryHiveString(RegistrySetting.Hive);

                // Apply the setting
                _registryService.SetValue(
                    $"{hiveString}\\{RegistrySetting.SubKey}",
                    RegistrySetting.Name,
                    RegistrySetting.DefaultValue,
                    RegistrySetting.ValueType);

                // Log the action
                _logService?.Log(LogLevel.Info, $"Restored setting {Name} to default value");
            }
            else if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
            {
                // Apply all linked settings
                foreach (var setting in LinkedRegistrySettings.Settings)
                {
                    // Get the registry hive string
                    string hiveString = GetRegistryHiveString(setting.Hive);

                    // Apply the setting
                    _registryService.SetValue(
                        $"{hiveString}\\{setting.SubKey}",
                        setting.Name,
                        setting.DefaultValue,
                        setting.ValueType);
                }

                // Log the action
                _logService?.Log(LogLevel.Info, $"Restored linked settings for {Name} to default values");
            }

            // Update the IsSelected property
            IsUpdatingFromCode = true;
            try
            {
                IsSelected = false;
            }
            finally
            {
                IsUpdatingFromCode = false;
            }

            // Refresh the status
            _ = RefreshStatus();
        }

        /// <summary>
        /// Refreshes the status of the setting.
        /// </summary>
        public async Task RefreshStatus()
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
                    RegistrySetting.Name);

                // Update the current value
                CurrentValue = currentValue;
                
                // Update the linked registry settings with values collection
                LinkedRegistrySettingsWithValues.Clear();
                LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(RegistrySetting, currentValue));

                // Determine if the value is null
                IsRegistryValueNull = currentValue == null;

                // Determine the status
                if (currentValue == null)
                {
                    // The value doesn't exist
                    Status = RegistrySetting.DefaultValue == null
                        ? RegistrySettingStatus.Applied
                        : RegistrySettingStatus.NotApplied;
                }
                else
                {
                    // Check if it matches the enabled value first
                    if (RegistrySetting.EnabledValue != null && currentValue.Equals(RegistrySetting.EnabledValue))
                    {
                        Status = RegistrySettingStatus.Applied;
                    }
                    // Then check if it matches the disabled value
                    else if (RegistrySetting.DisabledValue != null && currentValue.Equals(RegistrySetting.DisabledValue))
                    {
                        Status = RegistrySettingStatus.NotApplied;
                    }
                    // Finally, fall back to recommended value for backward compatibility
                    else if (currentValue.Equals(RegistrySetting.RecommendedValue))
                    {
                        // If RecommendedValue equals EnabledValue, mark as Applied
                        // If RecommendedValue equals DisabledValue, mark as NotApplied
                        if (RegistrySetting.EnabledValue != null && RegistrySetting.RecommendedValue.Equals(RegistrySetting.EnabledValue))
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
                StatusMessage = Status == RegistrySettingStatus.Applied
                    ? "Applied"
                    : "Not Applied";

                // Update the IsSelected property - only during initial load
                if (IsUpdatingFromCode)
                {
                    IsSelected = Status == RegistrySettingStatus.Applied;
                }
            }
            else if (LinkedRegistrySettings != null && LinkedRegistrySettings.Settings.Count > 0)
            {
                // Clear the existing values
                LinkedRegistrySettingsWithValues.Clear();
                
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
                        setting.Name);
                        
                    // Add to the linked registry settings with values collection
                    LinkedRegistrySettingsWithValues.Add(new LinkedRegistrySettingWithValue(setting, currentValue));

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
                        if (setting.EnabledValue != null && currentValue.Equals(setting.EnabledValue))
                        {
                            isApplied = true;
                        }
                        // Then check if it matches the disabled value
                        else if (setting.DisabledValue != null && currentValue.Equals(setting.DisabledValue))
                        {
                            isApplied = false;
                        }
                        // Finally, fall back to recommended value for backward compatibility
                        else if (currentValue.Equals(setting.RecommendedValue))
                        {
                            // If RecommendedValue equals EnabledValue, mark as Applied
                            // If RecommendedValue equals DisabledValue, mark as NotApplied
                            if (setting.EnabledValue != null && setting.RecommendedValue.Equals(setting.EnabledValue))
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
                    bool allRemoveActions = LinkedRegistrySettings.Settings.All(s => s.ActionType == RegistryActionType.Remove);
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
                    bool allRemoveActions = LinkedRegistrySettings.Settings.All(s => s.ActionType == RegistryActionType.Remove);
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
                StatusMessage = Status == RegistrySettingStatus.Applied
                    ? "Applied"
                    : "Not Applied";

                // Update the IsSelected property - only during initial load
                if (IsUpdatingFromCode)
                {
                    IsSelected = Status == RegistrySettingStatus.Applied;
                }
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
                    if (!string.IsNullOrWhiteSpace(value) && value.ToLowerInvariant().Contains(searchTerm))
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
    }
}
