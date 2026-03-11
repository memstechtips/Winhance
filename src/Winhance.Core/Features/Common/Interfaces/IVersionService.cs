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
    /// Downloads the installer for the latest version to a temp location.
    /// </summary>
    Task DownloadAndInstallUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches the downloaded installer and schedules the app to relaunch after installation.
    /// The caller should exit the application immediately after calling this.
    /// </summary>
    void LaunchInstallerAndRestart();
}
