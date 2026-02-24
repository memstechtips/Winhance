using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Handles WinGet CLI-based package install and uninstall operations.
/// </summary>
public interface IWinGetPackageInstaller
{
    Task<PackageInstallResult> InstallPackageAsync(string packageId, string? source = null, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string packageId, string? source = null, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default);
}
