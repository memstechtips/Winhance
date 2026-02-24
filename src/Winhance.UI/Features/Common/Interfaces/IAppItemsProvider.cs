using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Common interface for app item providers (Windows Apps, External Apps).
/// Decouples config services from concrete ViewModel types.
/// </summary>
public interface IAppItemsProvider
{
    bool IsInitialized { get; }
    Task LoadItemsAsync();
    ObservableCollection<AppItemViewModel> Items { get; }
}

/// <summary>
/// Provider for Windows Apps items with removal confirmation support.
/// </summary>
public interface IWindowsAppsItemsProvider : IAppItemsProvider
{
    Task<(bool Confirmed, bool SaveScripts)> ShowRemovalSummaryAndConfirm();
}

/// <summary>
/// Provider for External Apps items with install/uninstall support.
/// </summary>
public interface IExternalAppsItemsProvider : IAppItemsProvider
{
    Task InstallApps(bool skipConfirmation = false);
    Task UninstallAppsAsync();
}
