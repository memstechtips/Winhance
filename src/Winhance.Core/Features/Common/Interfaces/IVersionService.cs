using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IVersionService
{
    VersionInfo GetCurrentVersion();

    Task<VersionInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    Task DownloadAndInstallUpdateAsync(CancellationToken cancellationToken = default);

    // The caller should exit the application immediately after calling this.
    void LaunchInstallerAndRestart();
}
