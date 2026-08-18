namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// PackageManager COM -> WMI -> PowerShell fallback.
public interface IAppxPackageSource
{
    Task<HashSet<string>> GetInstalledPackageNamesAsync(CancellationToken cancellationToken = default);
}
