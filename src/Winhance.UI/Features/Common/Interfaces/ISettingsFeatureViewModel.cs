using System.Collections.ObjectModel;
using System.ComponentModel;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISettingsFeatureViewModel : INotifyPropertyChanged, IDisposable
{
    string ModuleId { get; }

    string DisplayName { get; }

    ObservableCollection<SettingItemViewModel> Settings { get; }

    bool HasVisibleSettings { get; }

    bool IsExpanded { get; set; }

    bool IsLoading { get; }

    int SettingsCount { get; }

    string GroupDescriptionText { get; }

    ObservableCollection<SettingsGroup> GroupedSettings { get; }

    Task LoadSettingsAsync();

    Task RefreshSettingsAsync();

    Task RefreshSettingStatesAsync();

    void ApplySearchFilter(string searchText);

}
