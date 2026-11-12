using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IInstalledAppsService
{
    // One read of the PC gives the installed apps, their updates and whether each definition is installed. DISM is
    // asked only when a definition names a capability or optional feature.
    Task<InstalledAppsScan> GetInstalledAppsAsync(IReadOnlyList<ItemDefinition> definitions, StatusRules rules, CancellationToken cancellationToken = default);

    // Call after anything that installs, removes or updates an app: the next scan reads the PC again.
    void Invalidate();

    Task<OperationResult<bool>> UpdateAsync(InstalledApp app, CancellationToken cancellationToken = default);

    // The Store apps among these with a process running from their package. Windows cannot swap an open app's
    // package, so its update only lands the next time it starts unless it is closed first.
    Task<InstalledApp[]> FindOpenAppsAsync(IReadOnlyList<InstalledApp> apps, CancellationToken cancellationToken = default);

    // Ends every process running from these apps' packages; unsaved work in them is lost.
    Task CloseAsync(IReadOnlyList<InstalledApp> apps, CancellationToken cancellationToken = default);
}
