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

    // Rewrites the image with only these editions; deleting an index in place would leave its files in the image.
    // Editions are named, not numbered: the rewrite renumbers the ones that stay.
    Task<EditionRemovalResult> KeepEditionsAsync(
        string workingDirectory,
        IReadOnlyCollection<string> editionNames,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    // Offline servicing mounts index 1, and DISM cannot mount an ESD.
    Task<EditionRemovalResult> KeepEditionAsWimAsync(
        string workingDirectory,
        string editionName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteImageFileAsync(
        string workingDirectory,
        ImageFormat format,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
