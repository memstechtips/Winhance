namespace Winhance.Core.Features.AdvancedTools.Models
{
    public record ImageDetectionResult
    {
        public ImageFormatInfo? WimInfo { get; init; }
        public ImageFormatInfo? EsdInfo { get; init; }

        public bool BothExist => WimInfo != null && EsdInfo != null;
        public bool NeitherExists => WimInfo == null && EsdInfo == null;
        public bool HasAnyFormat => WimInfo != null || EsdInfo != null;

        public ImageFormatInfo? PrimaryFormat => WimInfo ?? EsdInfo;
    }
}
