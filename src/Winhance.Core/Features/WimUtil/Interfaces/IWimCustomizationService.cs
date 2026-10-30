using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IWimCustomizationService
{
    // Either driver folder: servicing moves the $OEM$ drivers into the image, and the storage drivers stay for Setup.
    bool AreDriversOnMedia(string workingDirectory);

    Task<bool> AddDriversAsync(
        string workingDirectory,
        string? driverSourcePath = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<OperationResult> AddXmlToImageAsync(
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

    Task<UpdateInstallStepResult> EnsureUpdateInstallStepAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<UpdateInstallStepResult> EnsureUpdateInstallStepAsync(
        string xmlPath,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
