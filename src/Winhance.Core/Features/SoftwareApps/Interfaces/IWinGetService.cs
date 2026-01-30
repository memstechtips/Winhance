using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWinGetService
{
    Task<bool> InstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default);
    Task<bool> InstallWinGetAsync(CancellationToken cancellationToken = default);
    Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> IsPackageInstalledAsync(string packageId, CancellationToken cancellationToken = default);
    Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default);
    Task<bool> EnsureWinGetUpToDateAsync(IProgress<TaskProgressDetail> progress = null, CancellationToken cancellationToken = default);
    Task<string?> GetInstallerTypeAsync(string packageId, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);
}
