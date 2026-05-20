using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Provider for External Apps items with install/uninstall support.
/// Decouples config services from the concrete ViewModel type.
/// </summary>
public interface IExternalAppsItemsProvider
{
    bool IsInitialized { get; }
    Task LoadItemsAsync();
    ObservableCollection<AppItemViewModel> Items { get; }
    Task InstallApps(bool skipConfirmation = false);
    Task UninstallAppsAsync();
}
