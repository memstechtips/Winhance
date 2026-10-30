using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWinGetPackageInstaller
{
    Task<PackageInstallResult> InstallPackageAsync(string packageId, string? source, string? displayName, string? installerOverride, InstallOptions options, CancellationToken cancellationToken);
    Task<bool> DownloadPackageAsync(string packageId, string architecture, bool forAllUsers, string destinationFolder, string? displayName, CancellationToken cancellationToken);
    Task<PackageInstallResult> UpgradePackageAsync(string packageId, string? source, string? displayName, CancellationToken cancellationToken);
    Task<bool> UninstallPackageAsync(string packageId, string? source = null, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default);
}
