namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// COM API with CLI fallback.
public interface IWinGetDetectionService
{
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);
    Task<string?> GetInstallerTypeAsync(string packageId, CancellationToken cancellationToken = default);
}
