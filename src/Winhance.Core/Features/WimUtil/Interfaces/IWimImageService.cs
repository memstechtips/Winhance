using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IWimImageService
{
    Task<ImageFormatInfo?> DetectImageFormatAsync(string workingDirectory);

    Task<ImageDetectionResult> DetectAllImageFormatsAsync(string workingDirectory);

    Task<bool> ConvertImageAsync(
        string workingDirectory,
        ImageFormat targetFormat,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteImageFileAsync(
        string workingDirectory,
        ImageFormat format,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
