using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Pure UI state model for application settings.
    /// Contains ONLY UI-related properties and state - NO business logic.
    /// Replaces the massive ApplicationSettingItem god object with clean separation of concerns.
    /// </summary>
    public partial class SettingUIItem : ObservableObject, ISearchable
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
        private bool _isSelected;

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
        /// Whether this setting is visible in the UI (for filtering).
        /// </summary>
        [ObservableProperty]
        private bool _isVisible = true;

        /// <summary>
        /// Whether this item represents a group header rather than an actual setting.
        /// </summary>
        [ObservableProperty]
        private bool _isGroupHeader;

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
        /// Whether the current registry value is null (for display logic).
        /// </summary>
        [ObservableProperty]
        private bool _isRegistryValueNull;

        #endregion

        #region Control Type Properties

        /// <summary>
        /// The type of UI control to display for this setting.
        /// </summary>
        [ObservableProperty]
        private ControlType _controlType = ControlType.BinaryToggle;

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
    }
}
