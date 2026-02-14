using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWinGetService
{
    /// <summary>
    /// Raised after WinGet is successfully installed and the COM API is verified ready.
    /// Subscribers can use this to refresh installation status that depends on WinGet.
    /// </summary>
    event EventHandler? WinGetInstalled;

    Task<PackageInstallResult> InstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default);
    Task<bool> InstallWinGetAsync(CancellationToken cancellationToken = default);
    Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> IsPackageInstalledAsync(string packageId, CancellationToken cancellationToken = default);
    Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default);
    Task<bool> EnsureWinGetUpToDateAsync(IProgress<TaskProgressDetail> progress = null, CancellationToken cancellationToken = default);
    Task<string?> GetInstallerTypeAsync(string packageId, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);
}
