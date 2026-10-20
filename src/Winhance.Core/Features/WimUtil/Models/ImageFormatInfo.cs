namespace Winhance.Core.Features.WimUtil.Models;

public record ImageFormatInfo
{
    public ImageFormat Format { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public IReadOnlyList<ImageEdition> Editions { get; init; } = [];

    // Every edition on one medium shares a build; 0 when DISM did not report one.
    public int Build { get; init; }
}

// EditionId is the image's own id (Professional, CoreSingleLanguage), the same in every UI language.
public sealed record ImageEdition(int Index, string Name, string EditionId);

// LeftoverImage is the old image when it could not be deleted after the swap; left there, it goes onto the ISO or USB.
public sealed record EditionRemovalResult(bool Removed, string? LeftoverImage = null)
{
    public static EditionRemovalResult Failed { get; } = new(false);
}

public enum ImageFormat
{
    None,
    Wim,
    Esd
}
