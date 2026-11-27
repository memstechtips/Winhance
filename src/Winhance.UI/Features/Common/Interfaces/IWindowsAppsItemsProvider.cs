using System.Collections.ObjectModel;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

public interface IWindowsAppsItemsProvider
{
    bool IsInitialized { get; }
    Task LoadItemsAsync();
    ObservableCollection<AppItemViewModel> Items { get; }
    void ChangeSelection(Action change);
    Task<(bool Confirmed, bool SaveScripts)> ShowRemovalSummaryAndConfirm();
}
