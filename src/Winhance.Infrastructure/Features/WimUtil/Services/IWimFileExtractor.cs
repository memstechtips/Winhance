namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface IWimFileExtractor
{
    // Returns the image paths that were found and extracted. A path the image lacks is skipped, so a
    // caller can ask for an optional file; anything else that goes wrong throws.
    IReadOnlyList<string> Extract(
        string wimPath,
        int imageIndex,
        IReadOnlyDictionary<string, string> destinationByImagePath);
}
