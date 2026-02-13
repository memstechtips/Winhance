using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Extracts the regedit.exe icon using P/Invoke and caches it as a WinUI3-compatible SoftwareBitmapSource.
/// Must be called from the UI thread (SoftwareBitmapSource is dispatcher-bound).
/// </summary>
public static class RegeditIconProvider
{
    private static SoftwareBitmapSource? _cachedIcon;
    private static bool _attempted;

    public static SoftwareBitmapSource? CachedIcon => _cachedIcon;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int nCount, ref BITMAP lpObject);

    [DllImport("gdi32.dll")]
    private static extern int GetBitmapBits(IntPtr hBitmap, int cb, byte[] lpBits);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

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

    public static async Task GetIconAsync()
    {
        if (_attempted) return;
        _attempted = true;

        try
        {
            var regeditPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "regedit.exe");

            var hIcon = ExtractIcon(IntPtr.Zero, regeditPath, 0);
            if (hIcon == IntPtr.Zero || hIcon == (IntPtr)1)
                return;

            try
            {
                if (!GetIconInfo(hIcon, out var iconInfo))
                    return;

                try
                {
                    if (iconInfo.hbmColor == IntPtr.Zero)
                        return;

                    var bmp = new BITMAP();
                    GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), ref bmp);

                    if (bmp.bmWidth <= 0 || bmp.bmHeight <= 0)
                        return;

                    int width = bmp.bmWidth;
                    int height = bmp.bmHeight;
                    int stride = width * 4; // BGRA
                    var bits = new byte[stride * height];
                    GetBitmapBits(iconInfo.hbmColor, bits.Length, bits);

                    // GDI bitmaps are bottom-up — flip vertically
                    var flipped = new byte[bits.Length];
                    for (int y = 0; y < height; y++)
                    {
                        Array.Copy(bits, (height - 1 - y) * stride, flipped, y * stride, stride);
                    }

                    // Create SoftwareBitmap from pixel data
                    var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
                    softwareBitmap.CopyFromBuffer(flipped.AsBuffer());

                    var source = new SoftwareBitmapSource();
                    await source.SetBitmapAsync(softwareBitmap);
                    _cachedIcon = source;
                }
                finally
                {
                    if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
                    if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
                }
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            // Extraction failed — _cachedIcon remains null, XAML shows fallback icon
        }
    }
}
