using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IWimCustomizationService
{
    Task<bool> AddDriversAsync(
        string workingDirectory,
        string? driverSourcePath = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> AddXmlToImageAsync(
        string xmlPath,
        string workingDirectory);

    Task<string> DownloadUnattendedWinstallXmlAsync(
        string destinationPath,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<DriverInstallStepResult> EnsureDriverInstallStepAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<DriverInstallStepResult> EnsureDriverInstallStepAsync(
        string xmlPath,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
