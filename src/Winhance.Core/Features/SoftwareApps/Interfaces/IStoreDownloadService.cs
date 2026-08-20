namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// Uses the store.rg-adguard.net API to fetch packages from Microsoft's CDN, bypassing market restrictions.
public interface IStoreDownloadService
{
    Task<bool> DownloadAndInstallPackageAsync(string productId, string? displayName = null, CancellationToken cancellationToken = default);
}
