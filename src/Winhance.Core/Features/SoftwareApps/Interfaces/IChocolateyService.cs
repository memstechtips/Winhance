using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IChocolateyService
{
    Task<bool> IsChocolateyInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallChocolateyAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a stale Chocolatey package record without running the app's own uninstaller.
    /// Used after WinGet or Registry has already removed the app — Chocolatey doesn't notice
    /// out-of-band uninstalls, so its lib folder keeps reporting the package as installed
    /// and detection sees a ghost. No-op when Chocolatey isn't installed or doesn't list
    /// the package. Best-effort: returns false on failure but does not throw.
    /// </summary>
    Task<bool> CleanupStalePackageRecordAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
}
