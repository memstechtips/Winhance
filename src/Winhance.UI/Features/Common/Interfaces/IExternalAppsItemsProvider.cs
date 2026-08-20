using System.Collections.ObjectModel;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

public interface IExternalAppsItemsProvider
{
    bool IsInitialized { get; }
    Task LoadItemsAsync();
    ObservableCollection<AppItemViewModel> Items { get; }
    Task InstallApps(bool skipConfirmation = false);
    Task UninstallAppsAsync();
}
