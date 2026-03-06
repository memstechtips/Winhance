using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IVersionService
{
    /// <summary>
    /// Gets the current application version
    /// </summary>
    VersionInfo GetCurrentVersion();

    /// <summary>
    /// Checks if an update is available
    /// </summary>
    /// <returns>A task that resolves to true if an update is available, false otherwise</returns>
    Task<VersionInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and launches the installer for the latest version
    /// </summary>
    /// <returns>A task that completes when the download is initiated</returns>
    Task DownloadAndInstallUpdateAsync(CancellationToken cancellationToken = default);
}
