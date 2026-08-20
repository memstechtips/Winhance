using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWinGetPackageInstaller
{
    Task<PackageInstallResult> InstallPackageAsync(string packageId, string? source = null, string? displayName = null, string? installerOverride = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string packageId, string? source = null, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default);
}
