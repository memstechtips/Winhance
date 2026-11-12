using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Helpers;

namespace Winhance.UI.Features.Common.Services;

// With --diag-memory, one log line per navigation and mode change with the process's memory and how many tracked
// pages and view models are still alive. It forces a full GC before measuring, so the counts are exact. Without the
// flag it logs nothing and the tracker holds nothing.
public sealed class MemoryDiagnostics
{
    private const double MegaByte = 1024.0 * 1024.0;

    private readonly ILogService _logService;
    private readonly LaunchOptions _launch;
    private readonly IApplicationModeService _modeService;
    private readonly IConfigReviewModeService _reviewModeService;

    public MemoryDiagnostics(
        ILogService logService,
        LaunchOptions launch,
        IApplicationModeService modeService,
        IConfigReviewModeService reviewModeService)
    {
        _logService = logService;
        _launch = launch;
        _modeService = modeService;
        _reviewModeService = reviewModeService;
        _modeService.ModeChanged += (_, _) => Log($"mode {_modeService.CurrentMode}");
        _reviewModeService.ReviewModeChanged += (_, _) => Log($"review {(_reviewModeService.IsInReviewMode ? "enter" : "exit")}");
    }

    public void LogNavigation(Page? page)
    {
        if (page is not null)
            LiveInstanceTracker.Shared.RegisterOnce(page);
        Log($"nav {page?.GetType().Name ?? "none"}");
    }

    public void Log(string eventName)
    {
        if (!_launch.DiagMemory)
            return;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        var gc = GC.GetGCMemoryInfo();
        var alive = string.Join(' ', LiveInstanceTracker.Shared.AliveCounts().Select(c => $"{c.Key}={c.Value}"));
        _logService.LogInformation(string.Create(CultureInfo.InvariantCulture,
            $"[mem-gc] {eventName} | private={process.PrivateMemorySize64 / MegaByte:F0}MB ws={process.WorkingSet64 / MegaByte:F0}MB heap={gc.HeapSizeBytes / MegaByte:F0}MB committed={gc.TotalCommittedBytes / MegaByte:F0}MB allocated={GC.GetTotalAllocatedBytes() / MegaByte:F0}MB gc={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} | alive: {alive}"));
    }
}
