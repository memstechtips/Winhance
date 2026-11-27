namespace Winhance.Core.Features.Common.Interfaces;

public interface IReviewModeViewModelCoordinator
{
    bool HasSelectedWindowsApps { get; }

    bool HasSelectedExternalApps { get; }

    bool IsWindowsAppsInstallAction { get; }

    bool IsWindowsAppsRemoveAction { get; }

    bool IsExternalAppsInstallAction { get; }

    bool IsExternalAppsRemoveAction { get; }

    List<string> GetSelectedExternalAppIds();

    // For re-entering review mode while the singleton ViewModels still have settings loaded.
    void ReapplyReviewDiffsToExistingSettings();

    // Every page's Quick Actions Accept All at once, plus the import defaults for selected apps that have no action yet.
    void AcceptAllRemaining();

    Task RemoveWindowsAppsAsync(bool skipConfirmation, bool saveRemovalScripts);

    Task InstallWindowsAppsAsync();
}
