using System.Collections.ObjectModel;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// Represents a group of settings for display in a grouped ListView.
/// Implements IGrouping pattern expected by CollectionViewSource with IsSourceGrouped=True.
/// </summary>
public class SettingsGroup : ObservableCollection<SettingItemViewModel>
{
    /// <summary>
    /// Gets the group key (localized group name).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Creates a new settings group with the specified key.
    /// </summary>
    /// <param name="key">The group key (group name).</param>
    public SettingsGroup(string key) : base()
    {
        Key = key ?? string.Empty;
    }

    /// <summary>
    /// Creates a new settings group with the specified key and items.
    /// </summary>
    /// <param name="key">The group key (group name).</param>
    /// <param name="items">The settings in this group.</param>
    public SettingsGroup(string key, IEnumerable<SettingItemViewModel> items) : base(items)
    {
        Key = key ?? string.Empty;
    }
}
