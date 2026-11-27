using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigAppSelectionService
{
    Task SelectWindowsAppsFromConfigAsync(ConfigSection windowsAppsSection);
    Task<(bool shouldContinue, bool saveScripts)> ConfirmWindowsAppsRemovalAsync();
    Task ClearWindowsAppsSelectionAsync();
    Task SelectInstalledStateAsync();
    Task SelectExternalAppsFromConfigAsync(ConfigSection externalAppsSection);
    Task ClearExternalAppsSelectionAsync();
    Task ProcessExternalAppsInstallationAsync(ConfigSection externalAppsSection);
    Task ProcessExternalAppsRemovalAsync(ConfigSection externalAppsSection);
    Task ProcessExternalAppsFromUserSelectionAsync(List<string> selectedAppIds, bool remove);
}
