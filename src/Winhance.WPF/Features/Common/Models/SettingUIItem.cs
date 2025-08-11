using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Pure UI state model for application settings.
    /// Contains ONLY UI-related properties and state - NO business logic.
    /// Replaces the massive ApplicationSettingItem god object with clean separation of concerns.
    /// </summary>
    public partial class SettingUIItem : ObservableObject, ISettingItem, ISearchable
    {
        #region Core Properties

        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _groupName = string.Empty;

        [ObservableProperty]
        private string? _icon;

        #endregion

        #region UI State Properties

        /// <summary>
        /// Whether this setting is currently selected/enabled in the UI.
        /// This is PURE UI state - does NOT trigger business logic automatically.
        /// </summary>
        [ObservableProperty]
        private bool _isSelected = false;

        /// <summary>
        /// The currently selected value for ComboBox controls.
        /// This is PURE UI state - does NOT trigger business logic automatically.
        /// </summary>
        [ObservableProperty]
        private object? _selectedValue;

        /// <summary>
        /// Whether this setting is currently enabled/available for interaction.
        /// </summary>
        [ObservableProperty]
        private bool _isEnabled = true;

        /// <summary>
        /// Whether this setting is currently being applied (for loading indicators).
        /// </summary>
        [ObservableProperty]
        private bool _isApplying;

        /// <summary>
        /// The type of control to display for this setting.
        /// </summary>
        [ObservableProperty]
        private ControlType _controlType = ControlType.BinaryToggle;

        /// <summary>
        /// Whether this setting is visible in the UI (for filtering).
        /// </summary>
        [ObservableProperty]
        private bool _isVisible = true;

        /// <summary>
        /// Whether this item represents a group header rather than an actual setting.
        /// </summary>
        [ObservableProperty]
        private bool _isGroupHeader;

        /// <summary>
        /// Action to be called when this setting's selection state changes.
        /// This should be set by the coordinator to handle the actual setting application.
        /// </summary>
        public Func<bool, Task>? OnSettingChanged { get; set; }

        /// <summary>
        /// Action to be called when this setting's value changes (for comboboxes).
        /// This should be set by the coordinator to handle the actual setting application.
        /// </summary>
        public Func<object?, Task>? OnSettingValueChanged { get; set; }

        #endregion

        #region Display Properties

        /// <summary>
        /// Current status of the setting for display purposes.
        /// </summary>
        [ObservableProperty]
        private RegistrySettingStatus _status = RegistrySettingStatus.Unknown;

        /// <summary>
        /// User-friendly status message for display.
        /// </summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        /// <summary>
        /// Current value of the setting from the system (for display only).
        /// </summary>
        [ObservableProperty]
        private object? _currentValue;

        /// <summary>
        /// User-friendly display value for the current system value.
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

        /// <summary>
        /// Whether the registry value is null (key doesn't exist).
        /// </summary>
        [ObservableProperty]
        private bool _isRegistryValueNull;

        #endregion

        #region Tooltip Properties

        /// <summary>
        /// Single registry setting for tooltip display.
        /// This is populated from the first registry setting in the domain model.
        /// </summary>
        public RegistrySetting? RegistrySetting { get; set; }

        /// <summary>
        /// Collection of linked registry settings with their current values for tooltip display.
        /// This is populated when the setting has multiple registry settings.
        /// </summary>
        public ObservableCollection<LinkedRegistrySettingWithValue> LinkedRegistrySettingsWithValues { get; set; } = new();

        /// <summary>
        /// Collection of command settings for tooltip display.
        /// This is populated from the domain model's command settings.
        /// </summary>
        public List<CommandSetting> CommandSettings { get; set; } = new();

        /// <summary>
        /// Gets a value indicating whether this setting only has command settings (no registry settings).
        /// Used by the tooltip template to determine display logic.
        /// </summary>
        public bool HasCommandSettingsOnly
        {
            get
            {
                bool hasRegistrySettings = RegistrySetting != null || LinkedRegistrySettingsWithValues.Count > 0;
                bool hasCommandSettings = CommandSettings.Count > 0;
                return hasCommandSettings && !hasRegistrySettings;
            }
        }

        /// <summary>
        /// Gets a value indicating whether there are no settings to display in the tooltip.
        /// Used by the tooltip template to show a "no settings" message.
        /// </summary>
        public bool HasNoSettings
        {
            get
            {
                bool hasRegistrySettings = RegistrySetting != null || LinkedRegistrySettingsWithValues.Count > 0;
                bool hasCommandSettings = CommandSettings.Count > 0;
                return !hasRegistrySettings && !hasCommandSettings;
            }
        }

        #endregion

        #region Control Type Properties

        /// <summary>
        /// Number of steps for slider controls.
        /// </summary>
        [ObservableProperty]
        private int? _sliderSteps;

        /// <summary>
        /// Current value for slider controls.
        /// </summary>
        [ObservableProperty]
        private int _sliderValue;

        /// <summary>
        /// Labels for slider steps.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _sliderLabels = new();

        /// <summary>
        /// Available options for ComboBox controls.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _comboBoxOptions = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor for design-time and simple initialization.
        /// </summary>
        public SettingUIItem()
        {
            InitializeApplyCommand();
        }

        /// <summary>
        /// Constructor with basic required properties.
        /// </summary>
        /// <param name="id">Unique identifier for the setting.</param>
        /// <param name="name">Display name for the setting.</param>
        /// <param name="description">Description of the setting.</param>
        /// <param name="groupName">Group name for organizing settings.</param>
        public SettingUIItem(string id, string name, string description, string groupName)
        {
            Id = id;
            Name = name;
            FullName = name;
            Description = description;
            GroupName = groupName;
            InitializeApplyCommand();
        }

        #endregion

        #region Property Change Handlers

        partial void OnNameChanged(string value)
        {
            FullName = value;
        }

        partial void OnCurrentValueChanged(object? value)
        {
            IsRegistryValueNull = value == null;
            OnPropertyChanged(nameof(DisplayValue));
        }

        partial void OnSelectedValueChanged(object? value)
        {
            // Handle value-based controls (ComboBox, NumericUpDown, Slider)
            if (OnSettingValueChanged != null && !IsApplying)
            {
                switch (ControlType)
                {
                    case ControlType.ComboBox:
                        // Convert display string back to numeric index for service layer
                        var numericValue = GetNumericValueFromDisplayString(value?.ToString());
                        _ = OnSettingValueChanged(numericValue);
                        break;
                        
                    case ControlType.NumericUpDown:
                    case ControlType.Slider:
                        // Pass the value directly for numeric controls
                        _ = OnSettingValueChanged(value);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Converts a ComboBox display string back to its numeric index.
        /// </summary>
        /// <param name="displayString">The display string selected in the ComboBox.</param>
        /// <returns>The numeric index corresponding to the display string.</returns>
        private int GetNumericValueFromDisplayString(string? displayString)
        {
            if (string.IsNullOrEmpty(displayString) || ComboBoxOptions.Count == 0)
                return 0;
                
            // Find the index of the display string in ComboBoxOptions
            var index = ComboBoxOptions.ToList().IndexOf(displayString);
            return index >= 0 ? index : 0;
        }

        partial void OnIsSelectedChanged(bool value)
        {
            // Only trigger setting change if not during initialization/refresh
            if (OnSettingChanged != null && !IsApplying)
            {
                _ = OnSettingChanged(value);
            }
        }

        #endregion

        #region ISearchable Implementation

        /// <summary>
        /// Determines if this setting matches the given search term.
        /// </summary>
        /// <param name="searchTerm">The search term to match against.</param>
        /// <returns>True if the setting matches the search term, false otherwise.</returns>
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
                    if (!string.IsNullOrWhiteSpace(value) && 
                        value.ToLowerInvariant().Contains(searchTerm))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the properties that should be searched when filtering settings.
        /// </summary>
        /// <returns>An array of property names that should be searched.</returns>
        public virtual string[] GetSearchableProperties()
        {
            return new[] { nameof(Name), nameof(Description), nameof(GroupName) };
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Updates the UI state from external sources without triggering property change events.
        /// This should only be used during initialization or refresh operations.
        /// </summary>
        /// <param name="isSelected">Whether the setting should be selected.</param>
        /// <param name="selectedValue">The value that should be selected.</param>
        /// <param name="status">The current status of the setting.</param>
        /// <param name="currentValue">The current value from the system.</param>
        public void UpdateUIStateFromSystem(bool isSelected, object? selectedValue, RegistrySettingStatus status, object? currentValue)
        {
            // Update properties without triggering change notifications during initialization
            SetProperty(ref _isSelected, isSelected, nameof(IsSelected));
            SetProperty(ref _selectedValue, selectedValue, nameof(SelectedValue));
            SetProperty(ref _status, status, nameof(Status));
            SetProperty(ref _currentValue, currentValue, nameof(CurrentValue));
            
            // Update derived properties
            IsRegistryValueNull = currentValue == null;
            StatusMessage = GetStatusMessage(status);
            
            OnPropertyChanged(nameof(DisplayValue));
            
            // Notify tooltip-related computed properties that they may have changed
            OnPropertyChanged(nameof(HasCommandSettingsOnly));
            OnPropertyChanged(nameof(HasNoSettings));
        }

        /// <summary>
        /// Gets a user-friendly status message for the given status.
        /// </summary>
        /// <param name="status">The registry setting status.</param>
        /// <returns>A user-friendly status message.</returns>
        private string GetStatusMessage(RegistrySettingStatus status)
        {
            return status switch
            {
                RegistrySettingStatus.Applied => "Applied",
                RegistrySettingStatus.NotApplied => "Not Applied",
                RegistrySettingStatus.Modified => "Modified",
                RegistrySettingStatus.Error => "Error",
                RegistrySettingStatus.Unknown => "Unknown",
                _ => "Unknown"
            };
        }

        #endregion

        #region ISettingItem Implementation

        /// <summary>
        /// Gets or sets the dependencies for this setting.
        /// </summary>
        public List<SettingDependency> Dependencies { get; set; } = new();

        /// <summary>
        /// Gets the command to apply the setting.
        /// This delegates to the OnSettingChanged action if available.
        /// </summary>
        public ICommand ApplySettingCommand { get; private set; }

        /// <summary>
        /// Initializes the ApplySettingCommand.
        /// </summary>
        private void InitializeApplyCommand()
        {
            ApplySettingCommand = new AsyncRelayCommand(async () =>
            {
                if (OnSettingChanged != null)
                {
                    await OnSettingChanged(IsSelected);
                }
            });
        }

        #endregion
    }
}
