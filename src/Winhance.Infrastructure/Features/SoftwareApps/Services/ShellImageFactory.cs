using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Windows-only implementation of <see cref="IShellImageFactory"/>. Wraps the
/// Shell COM interface <c>IShellItemImageFactory</c> (the same path File Explorer
/// uses to render thumbnails) and re-encodes the returned HBITMAP as PNG bytes.
/// </summary>
// 10.0.10240.0 = Windows 10 RTM, the floor for SoftwareBitmap / BitmapEncoder.
// Tighter than the project TFM (10.0.19041.0) is fine; the analyzer just needs
// a version on the attribute so CA1416 stops flagging the WinRT call sites.
[SupportedOSPlatform("windows10.0.10240.0")]
public class ShellImageFactory : IShellImageFactory
{
    private readonly ILogService _logService;

    public ShellImageFactory(ILogService logService)
    {
        _logService = logService;
    }

    public Task<byte[]> GetIconBytesAsync(string filePath, Size size, CancellationToken ct = default)
    {
        // IShellItemImageFactory requires single-threaded apartment (STA). The default
        // .NET thread-pool (which Task.Run uses) is MTA, which is why GetImage returns
        // E_FAIL even for valid file paths. Spin a dedicated STA thread for the call;
        // SetApartmentState before Start() handles CoInitialize for that thread. Encoder
        // work runs on the same STA thread to avoid HBITMAP cross-thread marshalling.
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var bytes = ExtractAndEncodeOnSta(filePath, size, ct);
                tcs.TrySetResult(bytes);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled(ct);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = $"ShellImageFactory-STA";
        thread.Start();

        return tcs.Task;
    }

    private static byte[] ExtractAndEncodeOnSta(string filePath, Size size, CancellationToken ct)
    {
        // SHCreateItemFromParsingName(filePath, null, IShellItemImageFactory_GUID, out item)
        var iidShellItemImageFactory = new Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B");
        int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iidShellItemImageFactory, out var factoryObj);
        if (hr != 0 || factoryObj is null)
            throw new InvalidOperationException($"SHCreateItemFromParsingName failed (hr=0x{hr:X8}) for '{filePath}'");

        var factory = (IShellItemImageFactory)factoryObj;
        try
        {
            int width = (int)Math.Round(size.Width);
            int height = (int)Math.Round(size.Height);
            var sizePoint = new SIZE { cx = width, cy = height };

            // SIIGBF flag bits (from shobjidl_core.h):
            //   SIIGBF_RESIZETOFIT  = 0x00
            //   SIIGBF_BIGGERSIZEOK = 0x01  ← let the shell return a larger asset if needed
            //   SIIGBF_MEMORYONLY   = 0x02
            //   SIIGBF_ICONONLY     = 0x04
            //   SIIGBF_THUMBNAILONLY= 0x08
            //   SIIGBF_INCACHEONLY  = 0x10  ← "only return cached icons" — DO NOT use unconditionally
            // The first cut had BiggerSizeOk = 0x10 (which is actually IncacheOnly),
            // so every uncached path failed with E_FAIL. We want BiggerSizeOk = 0x01.
            const int siigbfResizeToFit = 0x00;
            const int siigbfBiggerSizeOk = 0x01;
            hr = factory.GetImage(sizePoint, siigbfResizeToFit | siigbfBiggerSizeOk, out IntPtr hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
                throw new InvalidOperationException($"IShellItemImageFactory.GetImage failed (hr=0x{hr:X8}) for '{filePath}'");

            try
            {
                return EncodeHBitmapAsPng(hBitmap, ct);
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>
    /// Reads the HBITMAP via GetDIBits → BGRA8 buffer → SoftwareBitmap → PNG bytes.
    /// Goes through the same WinRT BitmapEncoder as the trim pipeline so output
    /// format matches the rest of the cache. Synchronous (called on the STA thread):
    /// the WinRT async calls are bridged via .GetAwaiter().GetResult(), which is
    /// safe because we own the STA thread for the duration of this call and the
    /// encoder doesn't post continuations back to it.
    /// </summary>
    private static byte[] EncodeHBitmapAsPng(IntPtr hBitmap, CancellationToken ct)
    {
        if (!GetBitmapDimensions(hBitmap, out int width, out int height))
            throw new InvalidOperationException("Could not read HBITMAP dimensions");

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // negative for top-down orientation
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
            }
        };
        byte[] pixels = new byte[width * height * 4];
        IntPtr hdc = GetDC(IntPtr.Zero);
        try
        {
            int rowsCopied = GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref bmi, 0);
            if (rowsCopied == 0)
                throw new InvalidOperationException("GetDIBits returned 0 rows");
        }
        finally { ReleaseDC(IntPtr.Zero, hdc); }

        // GetDIBits gives us BGRA in pre-multiplied-alpha-friendly layout (B,G,R,A).
        var swBitmap = Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);

        using var outStream = new InMemoryRandomAccessStream();
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream).AsTask().GetAwaiter().GetResult();
        encoder.SetSoftwareBitmap(swBitmap);
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();

        outStream.Seek(0);
        using var managed = outStream.AsStreamForRead();
        using var collector = new MemoryStream();
        managed.CopyTo(collector);
        ct.ThrowIfCancellationRequested();
        return collector.ToArray();
    }

    private static bool GetBitmapDimensions(IntPtr hBitmap, out int width, out int height)
    {
        var bm = new BITMAP();
        int size = Marshal.SizeOf<BITMAP>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            int copied = GetObject(hBitmap, size, ptr);
            if (copied == 0) { width = 0; height = 0; return false; }
            bm = Marshal.PtrToStructure<BITMAP>(ptr);
            width = bm.bmWidth;
            height = bm.bmHeight;
            return true;
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    // --- COM interop ---------------------------------------------------------

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object factory);

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx; public int cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        // bmiColors omitted — we use BI_RGB which has no color table.
    }

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr h, int nCount, IntPtr lpObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines,
        [Out] byte[] lpvBits, ref BITMAPINFO lpbmi, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
}
