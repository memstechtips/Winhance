using Windows.Win32;
using Windows.Win32.System.Memory;
using Windows.Win32.System.SystemServices;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Utilities;

// HeapOptimizeResources asks every heap in the process to decommit the free blocks it keeps cached for reuse, so a
// page that built and dropped thousands of elements does not leave its peak committed.
internal static class NativeHeaps
{
    private static IDebounceTimer? _release;

    // Dropped elements are freed only when the GC collects their wrappers, and even then the heaps keep the freed
    // blocks committed for reuse, so a view switch, a re-split or leaving a page leaves its peak behind. The wait
    // lets the teardown finish, and a newer request (a zoom step, another switch) restarts it.
    public static void ReleaseDroppedMemoryLater(IDispatcherService dispatcherService, ILogService logService)
    {
        _release ??= dispatcherService.CreateDebounceTimer(TimeSpan.FromSeconds(5), () => Task.Run(Release).FireAndForget(logService));
        _release.Restart();
    }

    // WaitForPendingFinalizers runs off the UI thread: XAML wrappers release through it. The second collect reclaims
    // the wrappers those finalizers just let go, the same collect, drain, collect order PowerToys uses.
    private static void Release()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        ReleaseFreeMemory();
    }

    // The tray host's, once it has gone quiet: it only waits for the next event, so after the release the pages its
    // last event touched leave RAM too. Any it needs again fault back in.
    public static void ReleaseAllWhileIdle()
    {
        Release();
        PInvoke.SetProcessWorkingSetSize(PInvoke.GetCurrentProcess_SafeHandle(), nuint.MaxValue, nuint.MaxValue);
    }

    public static unsafe void ReleaseFreeMemory()
    {
        var info = new HEAP_OPTIMIZE_RESOURCES_INFORMATION { Version = PInvoke.HEAP_OPTIMIZE_RESOURCES_CURRENT_VERSION };
        PInvoke.HeapSetInformation(default, HEAP_INFORMATION_CLASS.HeapOptimizeResources, &info, (nuint)sizeof(HEAP_OPTIMIZE_RESOURCES_INFORMATION));
    }
}
