namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IChocolateyService
{
    Task<bool> IsChocolateyInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallChocolateyAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);

    // Chocolatey doesn't notice out-of-band uninstalls (WinGet / Registry), so its lib folder keeps reporting the
    // package and detection sees a ghost. No-op when Chocolatey isn't installed or doesn't list the package;
    // returns false on failure, never throws.
    Task<bool> CleanupStalePackageRecordAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
}
