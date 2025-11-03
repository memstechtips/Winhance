namespace Winhance.Core.Features.AdvancedTools.Models
{
    public class ImageFormatInfo
    {
        public ImageFormat Format { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int ImageCount { get; set; }
        public List<string> EditionNames { get; set; } = new();
    }

    public enum ImageFormat
    {
        None,
        Wim,
        Esd
    }
}
