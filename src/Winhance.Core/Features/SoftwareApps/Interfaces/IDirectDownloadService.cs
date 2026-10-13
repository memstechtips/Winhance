using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IDirectDownloadService
{
    Task<bool> DownloadAndInstallAsync(
        ItemDefinition item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    // The installer for another machine's architecture (x64, arm64, x86), into a folder it keeps; null when none downloads.
    Task<string?> DownloadAsync(
        ItemDefinition item,
        string architecture,
        string destinationFolder,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<string?> DownloadFileAsync(
        string url,
        string destinationFolder,
        string displayName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> InstallFileAsync(
        string filePath,
        string displayName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
