using System.Collections.ObjectModel;
using System.ComponentModel;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISettingsFeatureViewModel : INotifyPropertyChanged, IDisposable
{
    string ModuleId { get; }

    string DisplayName { get; }

    ObservableCollection<SettingItemViewModel> Settings { get; }

    bool HasVisibleSettings { get; }

    bool IsLoading { get; }

    // Set when an authoring mode is left; the cards still show authored values until the feature is rebuilt.
    bool NeedsRebuild { get; }

    int SettingsCount { get; }

    string GroupDescriptionText { get; }

    ObservableCollection<SettingsGroup> GroupedSettings { get; }

    Task LoadSettingsAsync();

    Task RefreshSettingsAsync();

    Task RefreshSettingStatesAsync();

    Task RefreshOnNavigationAsync();

    void ApplySearchFilter(string searchText);

    void SetViewFilter(SettingStateFilter stateFilter, Func<string, bool>? onlyChanges);

}
