namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IChocolateyService
{
    Task<bool> IsChocolateyInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallChocolateyAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);

    // Package ids from the community repository, through choco itself (Chocolatey asks callers not to query its feed
    // directly). Empty when Chocolatey is not installed or the search fails.
    Task<IReadOnlyList<string>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default);

    // Package id -> newer version for every installed package with an update, pinned ones left out. Empty when
    // Chocolatey is not installed, null when the check fails.
    Task<IReadOnlyDictionary<string, string>?> GetOutdatedPackagesAsync(CancellationToken cancellationToken = default);

    Task<bool> UpgradePackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);

    // Chocolatey doesn't notice out-of-band uninstalls (WinGet / Registry), so its lib folder keeps reporting the
    // package and detection sees a ghost. No-op when Chocolatey isn't installed or doesn't list the package;
    // returns false on failure, never throws.
    Task<bool> CleanupStalePackageRecordAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
}
