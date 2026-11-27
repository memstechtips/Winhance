using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.WimUtil.Models;

// What to build into the mounted image and what to clear out of it; each flag names one child of the Cleanup step.
public sealed record ImageCleanupOptions(
    bool ResetBase = false,
    bool DownloadedUpdates = false,
    bool ServicingLogs = false,
    bool TempFiles = false,
    bool CrashReports = false)
{
    public static ImageCleanupOptions None { get; } = new();

    public bool Any => ResetBase || DownloadedUpdates || ServicingLogs || TempFiles || CrashReports;
}

// EditionIndex is the image index to keep; null means the media holds one edition. Selections null means no
// apps or settings are built into the image. OutputFormat is the format the final export writes, so an ESD
// choice costs no second pass over the image.
public sealed record OfflineBuildRequest(string WorkingDirectory)
{
    public int? EditionIndex { get; init; }

    // Create lists its steps before the drivers and update steps put their files on the media; these say they will.
    public bool StagesDrivers { get; init; }

    public bool StagesUpdate { get; init; }

    public bool EnableDotNet35 { get; init; }

    public bool RemoveDotNetSource { get; init; }

    public ImageCleanupOptions Cleanup { get; init; } = ImageCleanupOptions.None;

    public SelectionSet? Selections { get; init; }

    public ImageFormat OutputFormat { get; init; } = ImageFormat.Wim;
}
