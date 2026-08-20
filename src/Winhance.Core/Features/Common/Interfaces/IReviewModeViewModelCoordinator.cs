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

    Task RemoveWindowsAppsAsync(bool skipConfirmation, bool saveRemovalScripts);

    Task InstallWindowsAppsAsync();
}
