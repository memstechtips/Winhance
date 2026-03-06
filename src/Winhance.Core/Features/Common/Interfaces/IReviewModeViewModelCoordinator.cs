using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Coordinates ViewModel interactions for review mode operations.
/// Abstracts concrete ViewModel dependencies so services can be unit-tested.
/// </summary>
public interface IReviewModeViewModelCoordinator
{
    /// <summary>Returns true if any Windows Apps are selected for operation.</summary>
    bool HasSelectedWindowsApps { get; }

    /// <summary>Returns true if any External Apps are selected for operation.</summary>
    bool HasSelectedExternalApps { get; }

    /// <summary>Whether Windows Apps tab is in install action mode.</summary>
    bool IsWindowsAppsInstallAction { get; }

    /// <summary>Whether Windows Apps tab is in remove action mode.</summary>
    bool IsWindowsAppsRemoveAction { get; }

    /// <summary>Whether External Apps tab is in install action mode.</summary>
    bool IsExternalAppsInstallAction { get; }

    /// <summary>Whether External Apps tab is in remove action mode.</summary>
    bool IsExternalAppsRemoveAction { get; }

    /// <summary>Gets IDs of currently selected external apps for preservation across review mode transitions.</summary>
    List<string> GetSelectedExternalAppIds();

    /// <summary>Clears all external app selections.</summary>
    void ClearExternalAppSelections();

    /// <summary>
    /// Reapplies review diffs to all existing loaded SettingItemViewModels.
    /// Called when re-entering review mode while singleton VMs still have settings loaded.
    /// </summary>
    void ReapplyReviewDiffsToExistingSettings();

    /// <summary>
    /// Removes apps via the Windows Apps ViewModel (runs on UI thread).
    /// </summary>
    Task RemoveWindowsAppsAsync(bool skipConfirmation, bool saveRemovalScripts);

    /// <summary>
    /// Installs apps via the Windows Apps ViewModel.
    /// </summary>
    Task InstallWindowsAppsAsync();
}
