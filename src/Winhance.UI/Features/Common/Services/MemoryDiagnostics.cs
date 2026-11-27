using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Helpers;

namespace Winhance.UI.Features.Common.Services;

// With --diag, one log line per navigation, mode change, minute and exit with the CPU time spent since the last line,
// the process's memory and where its native part sits, how many tracked pages and view models are still alive, and the
// element count of the visual tree. It forces a full GC before measuring, so the counts are exact. Without the flag it logs nothing and the tracker
// holds nothing.
public sealed class MemoryDiagnostics
{
    private const double MegaByte = 1024.0 * 1024.0;
    private const int TopTypes = 8;
    private const int TopTypesOnExit = 30;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);

    private readonly ILogService _logService;
    private readonly LaunchOptions _launch;
    private readonly IApplicationModeService _modeService;
    private readonly IConfigReviewModeService _reviewModeService;
    private readonly NativeMemoryBreakdown _native = new();
    private Window? _window;
    private DispatcherQueueTimer? _sampleTimer;
    private TimeSpan _cpuAtLastLine;
    private TimeSpan _uptimeAtLastLine;

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

    // Lets each line count the window's visual tree, and samples once a minute so growth with no clicks shows too.
    public void Attach(Window window)
    {
        if (!_launch.Diagnostics)
            return;

        _window = window;
        _sampleTimer = window.DispatcherQueue.CreateTimer();
        _sampleTimer.Interval = SampleInterval;
        _sampleTimer.Tick += (_, _) => Log("tick");
        _sampleTimer.Start();
    }

    public void LogNavigation(Page? page)
    {
        if (page is not null)
            LiveInstanceTracker.Shared.RegisterOnce(page);
        Log($"nav {page?.GetType().Name ?? "none"}");
        if (page is null || !_launch.Diagnostics)
            return;

        // The nav line runs before the page builds; this one counts the page once it and its async content are up.
        void OnLoaded(object sender, RoutedEventArgs e)
        {
            page.Loaded -= OnLoaded;
            LogLoadedPageAsync(page).FireAndForget(_logService);
        }
        page.Loaded += OnLoaded;
    }

    private async Task LogLoadedPageAsync(Page page)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        Log($"page {page.GetType().Name}");
    }

    public void LogExit() => Log("exit", TopTypesOnExit);

    // The long list names the row and card types too, so a section's count can be read per row.
    public void LogSection(string sectionKey) => Log($"section {sectionKey}", TopTypesOnExit);

    public void Log(string eventName) => Log(eventName, TopTypes);

    private void Log(string eventName, int topTypes)
    {
        if (!_launch.Diagnostics)
            return;

        // CPU is read before the forced GC and the baseline is reset after the line, so the work this method does is
        // never billed to the next event.
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime - _cpuAtLastLine;
        var span = DateTime.Now - process.StartTime - _uptimeAtLastLine;
        var corePercent = span > TimeSpan.Zero ? cpu / span * 100 : 0;

        // Aggressive compacts the large object heap and hands its free regions back. A plain full GC leaves them
        // committed, so private bytes swung by up to 10 MB with where the last big array happened to land.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        var gc = GC.GetGCMemoryInfo(GCKind.FullBlocking);
        var generations = gc.GenerationInfo;
        var alive = string.Join(' ', LiveInstanceTracker.Shared.AliveCounts().Select(c => $"{c.Key}={c.Value}"));
        var visual = VisualTypeCounts() is { } counts ? FormatCounts(counts, topTypes) : "n/a";

        // XAML elements, composition surfaces and decoded images live in native memory, so private minus committed
        // managed memory is the part the managed counts cannot explain.
        _logService.LogInformation(string.Create(CultureInfo.InvariantCulture,
            $"[mem-gc] {eventName} | cpu=+{cpu.TotalSeconds:F2}s in {span.TotalSeconds:F1}s ({corePercent:F0}% of a core, {Environment.ProcessorCount} cores) total={process.TotalProcessorTime.TotalSeconds:F1}s | private={process.PrivateMemorySize64 / MegaByte:F0}MB ws={process.WorkingSet64 / MegaByte:F0}MB heap={gc.HeapSizeBytes / MegaByte:F0}MB committed={gc.TotalCommittedBytes / MegaByte:F0}MB native={(process.PrivateMemorySize64 - gc.TotalCommittedBytes) / MegaByte:F0}MB allocated={GC.GetTotalAllocatedBytes() / MegaByte:F0}MB gc={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} | gen0={generations[0].SizeAfterBytes / MegaByte:F1}MB gen1={generations[1].SizeAfterBytes / MegaByte:F1}MB gen2={generations[2].SizeAfterBytes / MegaByte:F1}MB loh={generations[3].SizeAfterBytes / MegaByte:F1}MB poh={generations[4].SizeAfterBytes / MegaByte:F1}MB frag={gc.FragmentedBytes / MegaByte:F1}MB handles={process.HandleCount} threads={process.Threads.Count} | {_native.Describe(gc.TotalCommittedBytes)} | alive: {alive} | visual: {visual}"));

        process.Refresh();
        _cpuAtLastLine = process.TotalProcessorTime;
        _uptimeAtLastLine = DateTime.Now - process.StartTime;
    }

    internal static string FormatCounts(IReadOnlyDictionary<string, int> counts, int top)
    {
        var largest = counts
            .OrderByDescending(c => c.Value)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .Take(top)
            .Select(c => $"{c.Key}={c.Value}");
        return string.Join(' ', largest.Prepend($"total={counts.Values.Sum()}"));
    }

    // The tree can only be read on the UI thread; a line logged from elsewhere goes without it.
    private Dictionary<string, int>? VisualTypeCounts()
    {
        if (_window is not { Content: { XamlRoot: { } xamlRoot } content } || !_window.DispatcherQueue.HasThreadAccess)
            return null;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        CountTypes(content, counts);
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
        {
            if (popup.Child is { } child)
                CountTypes(child, counts);
        }

        return counts;
    }

    private static void CountTypes(DependencyObject element, Dictionary<string, int> counts)
    {
        var type = element.GetType().Name;
        counts[type] = counts.GetValueOrDefault(type) + 1;
        var children = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < children; i++)
            CountTypes(VisualTreeHelper.GetChild(element, i), counts);
    }
}
