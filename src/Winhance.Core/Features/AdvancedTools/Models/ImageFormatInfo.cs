namespace Winhance.Core.Features.AdvancedTools.Models
{
    public record ImageFormatInfo
    {
        public ImageFormat Format { get; init; }
        public string FilePath { get; init; } = string.Empty;
        public long FileSizeBytes { get; init; }
        public int ImageCount { get; init; }
        public IReadOnlyList<string> EditionNames { get; init; } = new List<string>();
    }

    public enum ImageFormat
    {
        None,
        Wim,
        Esd
    }
}
