using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// Represents a group of settings for display in a grouped ListView.
/// Implements IGrouping pattern expected by CollectionViewSource with IsSourceGrouped=True.
/// Tracks visibility of items to support hiding empty groups during search.
/// </summary>
public class SettingsGroup : ObservableCollection<SettingItemViewModel>
{
    private bool _hasVisibleItems = true;

    /// <summary>
    /// Gets the group key (localized group name).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets whether this group has any visible items.
    /// Used to hide group headers when all items are filtered out.
    /// </summary>
    public bool HasVisibleItems
    {
        get => _hasVisibleItems;
        private set
        {
            if (_hasVisibleItems != value)
            {
                _hasVisibleItems = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasVisibleItems)));
            }
        }
    }

    /// <summary>
    /// Creates a new settings group with the specified key.
    /// </summary>
    /// <param name="key">The group key (group name).</param>
    public SettingsGroup(string key) : base()
    {
        Key = key ?? string.Empty;
        CollectionChanged += OnCollectionChanged;
    }

    /// <summary>
    /// Creates a new settings group with the specified key and items.
    /// </summary>
    /// <param name="key">The group key (group name).</param>
    /// <param name="items">The settings in this group.</param>
    public SettingsGroup(string key, IEnumerable<SettingItemViewModel> items) : base(items)
    {
        Key = key ?? string.Empty;

        // Subscribe to visibility changes on all initial items
        foreach (var item in this)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        CollectionChanged += OnCollectionChanged;
        UpdateHasVisibleItems();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Unsubscribe from removed items
        if (e.OldItems != null)
        {
            foreach (SettingItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        // Subscribe to new items
        if (e.NewItems != null)
        {
            foreach (SettingItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        UpdateHasVisibleItems();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingItemViewModel.IsVisible))
        {
            UpdateHasVisibleItems();
        }
    }

    private void UpdateHasVisibleItems()
    {
        HasVisibleItems = this.Any(item => item.IsVisible);
    }
}
