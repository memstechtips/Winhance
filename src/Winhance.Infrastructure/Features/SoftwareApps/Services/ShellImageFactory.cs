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
[SupportedOSPlatform("windows")]
public class ShellImageFactory : IShellImageFactory
{
    private readonly ILogService _logService;

    public ShellImageFactory(ILogService logService)
    {
        _logService = logService;
    }

    public Task<byte[]> GetIconBytesAsync(string filePath, Size size, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            ct.ThrowIfCancellationRequested();

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

                // SIIGBF_RESIZETOFIT (0) | SIIGBF_BIGGERSIZEOK (0x10) — return at least
                // the requested size; let WinUI scale down if the source is larger.
                const int siigbfResizeToFit = 0x0;
                const int siigbfBiggerSizeOk = 0x10;
                hr = factory.GetImage(sizePoint, siigbfResizeToFit | siigbfBiggerSizeOk, out IntPtr hBitmap);
                if (hr != 0 || hBitmap == IntPtr.Zero)
                    throw new InvalidOperationException($"IShellItemImageFactory.GetImage failed (hr=0x{hr:X8}) for '{filePath}'");

                try
                {
                    return await EncodeHBitmapAsPngAsync(hBitmap, ct).ConfigureAwait(false);
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
        }, ct);
    }

    /// <summary>
    /// Reads the HBITMAP via GetDIBits → BGRA8 buffer → SoftwareBitmap → PNG bytes.
    /// Goes through the same WinRT BitmapEncoder as the trim pipeline so output
    /// format matches the rest of the cache.
    /// </summary>
    private static async Task<byte[]> EncodeHBitmapAsPngAsync(IntPtr hBitmap, CancellationToken ct)
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
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
        encoder.SetSoftwareBitmap(swBitmap);
        await encoder.FlushAsync();

        outStream.Seek(0);
        using var managed = outStream.AsStreamForRead();
        using var collector = new MemoryStream();
        await managed.CopyToAsync(collector, ct).ConfigureAwait(false);
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
