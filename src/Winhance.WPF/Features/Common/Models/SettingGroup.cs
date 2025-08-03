using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Represents a group of related settings for UI organization.
    /// Pure UI model with no business logic.
    /// </summary>
    public partial class SettingGroup : ObservableObject
    {
        /// <summary>
        /// The name of this setting group.
        /// </summary>
        [ObservableProperty]
        private string _name = string.Empty;

        /// <summary>
        /// Optional description for this group.
        /// </summary>
        [ObservableProperty]
        private string _description = string.Empty;

        /// <summary>
        /// Optional icon for this group (Material Symbols font character).
        /// </summary>
        [ObservableProperty]
        private string? _icon;

        /// <summary>
        /// Whether this group is currently expanded in the UI.
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded = true;

        /// <summary>
        /// Whether this group is visible in the UI (for filtering).
        /// </summary>
        [ObservableProperty]
        private bool _isVisible = true;

        /// <summary>
        /// The settings contained in this group.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<SettingUIItem> _settings = new();

        /// <summary>
        /// The number of settings in this group (for display).
        /// </summary>
        public int SettingsCount => Settings.Count;

        /// <summary>
        /// The number of selected/enabled settings in this group (for display).
        /// </summary>
        public int SelectedCount => Settings.Count(s => s.IsSelected);

        /// <summary>
        /// Whether all settings in this group are selected.
        /// </summary>
        public bool AllSelected => Settings.Count > 0 && Settings.All(s => s.IsSelected);

        /// <summary>
        /// Whether some (but not all) settings in this group are selected.
        /// </summary>
        public bool SomeSelected => Settings.Any(s => s.IsSelected) && !AllSelected;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SettingGroup()
        {
            Settings.CollectionChanged += (s, e) => 
            {
                OnPropertyChanged(nameof(SettingsCount));
                UpdateSelectionProperties();
            };
        }

        /// <summary>
        /// Constructor with name.
        /// </summary>
        /// <param name="name">The name of the group.</param>
        public SettingGroup(string name) : this()
        {
            Name = name;
        }

        /// <summary>
        /// Constructor with name and description.
        /// </summary>
        /// <param name="name">The name of the group.</param>
        /// <param name="description">The description of the group.</param>
        public SettingGroup(string name, string description) : this(name)
        {
            Description = description;
        }

        /// <summary>
        /// Adds a setting to this group and sets up property change notifications.
        /// </summary>
        /// <param name="setting">The setting to add.</param>
        public void AddSetting(SettingUIItem setting)
        {
            if (setting == null) return;

            Settings.Add(setting);
            setting.PropertyChanged += OnSettingPropertyChanged;
        }

        /// <summary>
        /// Removes a setting from this group.
        /// </summary>
        /// <param name="setting">The setting to remove.</param>
        public void RemoveSetting(SettingUIItem setting)
        {
            if (setting == null) return;

            if (Settings.Remove(setting))
            {
                setting.PropertyChanged -= OnSettingPropertyChanged;
            }
        }

        /// <summary>
        /// Selects or deselects all settings in this group.
        /// </summary>
        /// <param name="selected">Whether to select or deselect all settings.</param>
        public void SelectAll(bool selected)
        {
            foreach (var setting in Settings)
            {
                setting.IsSelected = selected;
            }
        }

        /// <summary>
        /// Updates the visibility of this group based on whether any settings are visible.
        /// </summary>
        public void UpdateVisibility()
        {
            IsVisible = Settings.Any(s => s.IsVisible);
        }

        /// <summary>
        /// Handles property changes from individual settings to update group-level properties.
        /// </summary>
        private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingUIItem.IsSelected))
            {
                UpdateSelectionProperties();
            }
            else if (e.PropertyName == nameof(SettingUIItem.IsVisible))
            {
                UpdateVisibility();
            }
        }

        /// <summary>
        /// Updates the selection-related properties based on individual setting states.
        /// </summary>
        private void UpdateSelectionProperties()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(AllSelected));
            OnPropertyChanged(nameof(SomeSelected));
        }
    }
}
