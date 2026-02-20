using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Service for downloading Microsoft Store packages directly, bypassing market restrictions.
/// Uses the store.rg-adguard.net API to download packages from Microsoft's CDN.
/// </summary>
public interface IStoreDownloadService
{
    /// <summary>
    /// Downloads and installs a Microsoft Store package by its Product ID.
    /// This method bypasses geographic market restrictions.
    /// </summary>
    /// <param name="productId">Microsoft Store Product ID (e.g., 9WZDNCRFHVN5)</param>
    /// <param name="displayName">Display name for progress reporting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if download and installation succeeded, false otherwise</returns>
    Task<bool> DownloadAndInstallPackageAsync(string productId, string? displayName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a Microsoft Store package to a specific directory without installing it.
    /// </summary>
    /// <param name="productId">Microsoft Store Product ID</param>
    /// <param name="downloadPath">Directory to save the downloaded package</param>
    /// <param name="displayName">Display name for progress reporting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the downloaded package file, or null if failed</returns>
    Task<string?> DownloadPackageAsync(string productId, string downloadPath, string? displayName = null, CancellationToken cancellationToken = default);
}
