namespace Winhance.Core.Features.Common.Selections;

// The one apps-to-choices reader: which apps the user has checked, and which Windows apps are installed.
public interface IAppSelectionSource
{
    Task<IReadOnlyList<AppChoice>> CheckedWindowsAppsAsync();
    Task<IReadOnlyList<AppChoice>> InstalledWindowsAppsAsync();
    Task<IReadOnlyList<AppChoice>> CheckedExternalAppsAsync();
}
