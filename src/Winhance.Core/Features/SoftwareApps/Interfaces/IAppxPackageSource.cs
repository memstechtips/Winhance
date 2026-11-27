using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// PackageManager COM -> WMI -> PowerShell fallback.
public interface IAppxPackageSource
{
    Task<HashSet<string>> GetInstalledPackageNamesAsync(CancellationToken cancellationToken = default);

    // PackageManager only: the WMI and PowerShell fallbacks return names, not the details the Installed Apps list shows.
    Task<IReadOnlyList<StorePackage>> GetStorePackagesAsync(CancellationToken cancellationToken = default);
}
