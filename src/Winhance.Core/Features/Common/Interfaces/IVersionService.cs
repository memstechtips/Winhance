using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IVersionService
{
    VersionInfo GetCurrentVersion();

    Task<VersionInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    // An empty Version when GitHub could not be reached.
    Task<VersionInfo> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

    Task DownloadAndInstallUpdateAsync(CancellationToken cancellationToken = default);

    bool IsUpdateDownloaded { get; }

    // The caller should exit the application immediately after calling either of these.
    void LaunchInstallerAndRestart(bool relaunchInTray = false);

    void LaunchInstallerOnExit();
}
