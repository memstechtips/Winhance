namespace Winhance.Core.Features.AdvancedTools.Models
{
    public class ImageDetectionResult
    {
        public ImageFormatInfo? WimInfo { get; set; }
        public ImageFormatInfo? EsdInfo { get; set; }

        public bool BothExist => WimInfo != null && EsdInfo != null;
        public bool NeitherExists => WimInfo == null && EsdInfo == null;
        public bool HasAnyFormat => WimInfo != null || EsdInfo != null;

        public ImageFormatInfo? PrimaryFormat => WimInfo ?? EsdInfo;
    }
}
