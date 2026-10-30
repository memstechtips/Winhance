using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// COM API with CLI fallback.
public interface IWinGetDetectionService
{
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WinGetInstalledPackage>?> GetInstalledPackagesAsync(CancellationToken cancellationToken = default);

    // Both installed lists come from one walk of winget's catalog that is kept for a short time. Call this after
    // anything that installs, removes or updates an app, so the next list is read again.
    void InvalidateInstalledPackages();
    Task<string?> GetInstallerTypeAsync(string packageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WinGetPackageMatch>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default);

    // Ids the winget source no longer has. Empty when the source cannot be reached: unknown is not missing.
    Task<IReadOnlyCollection<string>> FindMissingPackageIdsAsync(IReadOnlyCollection<string> packageIds, CancellationToken cancellationToken = default);
}
