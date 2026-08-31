using Winhance.Core.Features.AdvancedTools.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal static class UsbWriteLayoutPlanner
{
    // Microsoft's own figure in the "Create a bootable USB flash drive" instructions. Not 4000:
    // the split pieces have to clear FAT32's ceiling with headroom, not sit on it.
    internal const int SplitSizeMb = 3800;

    // FAT32 stores a file's size in 32 bits, so 4 GiB is the first size it cannot hold.
    internal const long Fat32MaxFileBytes = 4L * 1024 * 1024 * 1024;

    // "When using a FAT-32 drive, you can format it to only use 32GB of space" - Windows' own
    // formatter refuses more. It is both the partition size on a bigger stick and the payload
    // ceiling past which a second stick is needed. 32,000 MiB rather than exactly 32 GiB: the
    // refusal is a boundary check, and diskpart's size=32000 is the figure known to clear it.
    internal const long Fat32MaxVolumeBytes = 32_000L * 1024 * 1024;

    internal static UsbWriteLayout Plan(long totalPayloadBytes, long largestFileBytes)
    {
        return new UsbWriteLayout(
            RequiresSplit: largestFileBytes >= Fat32MaxFileBytes,
            SplitSizeMb: SplitSizeMb,
            TotalPayloadBytes: totalPayloadBytes,
            ExceedsFat32Ceiling: totalPayloadBytes > Fat32MaxVolumeBytes);
    }

    // Every .swm has to sit in the same folder as its siblings, and Setup looks for them beside
    // where install.wim would have been.
    internal static string SplitTargetPath(string mediaRoot) =>
        Path.Combine(mediaRoot, "sources", "install.swm");
}
