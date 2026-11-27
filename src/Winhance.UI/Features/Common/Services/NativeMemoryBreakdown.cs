using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.System.Memory;

namespace Winhance.UI.Features.Common.Services;

// Says where --diag's native memory sits. Committed private memory splits into the process heaps, the GC, and the
// rest: blocks taken straight from VirtualAlloc, which is where graphics drivers and the composition engine keep most
// of theirs. The video memory line is what each adapter holds for this process, and the DLL list shows which driver
// or COM server arrived with a jump.
internal sealed class NativeMemoryBreakdown
{
    private const double MegaByte = 1024.0 * 1024.0;
    private const int TopHeaps = 3;
    private readonly HashSet<string> _seenModules = new(StringComparer.OrdinalIgnoreCase);

    public string Describe(long gcCommittedBytes) => $"{Regions(gcCommittedBytes)} | {VideoMemory()} | new dlls: {NewModules()}";

    private static unsafe string Regions(long gcCommittedBytes)
    {
        long privateBytes = 0, mappedBytes = 0, imageBytes = 0;
        byte* address = null;
        MEMORY_BASIC_INFORMATION info;
        while (PInvoke.VirtualQuery(address, &info, (nuint)sizeof(MEMORY_BASIC_INFORMATION)) != 0)
        {
            if (info.State == VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT)
            {
                var size = (long)info.RegionSize;
                if (info.Type == PAGE_TYPE.MEM_PRIVATE)
                    privateBytes += size;
                else if (info.Type == PAGE_TYPE.MEM_MAPPED)
                    mappedBytes += size;
                else if (info.Type == PAGE_TYPE.MEM_IMAGE)
                    imageBytes += size;
            }

            address = (byte*)info.BaseAddress + info.RegionSize;
        }

        // In use is what the heap's callers still hold; the rest is freed space the heap keeps committed.
        var heaps = HeapSummaries();
        var heapBytes = heaps.Sum(h => h.Committed);
        var largest = heaps.MaxBy(h => h.Committed);
        var top = string.Join('/', heaps.Select(h => h.Committed).OrderDescending().Take(TopHeaps).Select(b => Mb(b)));
        return string.Create(CultureInfo.InvariantCulture,
            $"split: private={Mb(privateBytes)}MB heaps={Mb(heapBytes)}MB in {heaps.Count} (largest {top}, in use {Mb(largest.Allocated)}) gc={Mb(gcCommittedBytes)}MB other={Mb(privateBytes - heapBytes - gcCommittedBytes)}MB mapped={Mb(mappedBytes)}MB image={Mb(imageBytes)}MB");
    }

    private static unsafe List<(long Committed, long Allocated)> HeapSummaries()
    {
        // The raw overload: the friendly one wraps each heap in a SafeFileHandle, which would close it. A heap created
        // between the two calls is left out, never written past the buffer.
        var heaps = new HANDLE[PInvoke.GetProcessHeaps(0, (HANDLE*)null)];
        uint count;
        fixed (HANDLE* buffer = heaps)
            count = Math.Min(PInvoke.GetProcessHeaps((uint)heaps.Length, buffer), (uint)heaps.Length);

        var summaries = new List<(long Committed, long Allocated)>((int)count);
        for (var i = 0; i < count; i++)
        {
            var summary = new HEAP_SUMMARY { cb = (uint)sizeof(HEAP_SUMMARY) };
            if (PInvoke.HeapSummary(heaps[i], 0, &summary))
                summaries.Add(((long)summary.cbCommitted, (long)summary.cbAllocated));
        }

        return summaries;
    }

    // Local is the adapter's own memory, non-local the system memory it borrows ("Shared GPU memory" in Task Manager).
    private static string VideoMemory()
    {
        try
        {
            PInvoke.CreateDXGIFactory1(typeof(IDXGIFactory1).GUID, out var created).ThrowOnFailure();
            var factory = (IDXGIFactory1)created;
            var adapters = new List<string>();
            for (uint i = 0; TryGetAdapter(factory, i) is { } adapter; i++)
            {
                if (adapter is not IDXGIAdapter3 withBudget)
                    continue;

                var description = adapter.GetDesc1();
                withBudget.QueryVideoMemoryInfo(0, DXGI_MEMORY_SEGMENT_GROUP.DXGI_MEMORY_SEGMENT_GROUP_LOCAL, out var local);
                withBudget.QueryVideoMemoryInfo(0, DXGI_MEMORY_SEGMENT_GROUP.DXGI_MEMORY_SEGMENT_GROUP_NON_LOCAL, out var shared);
                adapters.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{description.Description.ToString().Trim()} local={Mb((long)local.CurrentUsage)}MB shared={Mb((long)shared.CurrentUsage)}MB"));
            }

            return "gpu: " + string.Join("; ", adapters);
        }
        catch (COMException ex)
        {
            return string.Create(CultureInfo.InvariantCulture, $"gpu: n/a (0x{ex.HResult:X8})");
        }
    }

    // EnumAdapters1 fails with DXGI_ERROR_NOT_FOUND one past the last adapter.
    private static IDXGIAdapter1? TryGetAdapter(IDXGIFactory1 factory, uint index)
    {
        try
        {
            factory.EnumAdapters1(index, out var adapter);
            return adapter;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private string NewModules()
    {
        using var process = Process.GetCurrentProcess();
        var added = new List<string>();
        foreach (ProcessModule module in process.Modules)
        {
            using (module)
            {
                if (_seenModules.Add(module.ModuleName))
                    added.Add(module.ModuleName);
            }
        }

        return added.Count == 0 ? "none" : string.Join(' ', added);
    }

    private static string Mb(long bytes) => (bytes / MegaByte).ToString("F0", CultureInfo.InvariantCulture);
}
