using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Provider for Windows Apps items with removal confirmation support.
/// Decouples config services from the concrete ViewModel type.
/// </summary>
public interface IWindowsAppsItemsProvider
{
    bool IsInitialized { get; }
    Task LoadItemsAsync();
    ObservableCollection<AppItemViewModel> Items { get; }
    Task<(bool Confirmed, bool SaveScripts)> ShowRemovalSummaryAndConfirm();
}
