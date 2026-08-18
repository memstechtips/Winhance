using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Winhance.UI.Features.Optimize.ViewModels;

// IGrouping shape expected by CollectionViewSource with IsSourceGrouped=True; tracks item visibility so empty
// groups hide during search.
public class SettingsGroup : ObservableCollection<SettingItemViewModel>
{
    private bool _hasVisibleItems = true;

    public string Key { get; }

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
