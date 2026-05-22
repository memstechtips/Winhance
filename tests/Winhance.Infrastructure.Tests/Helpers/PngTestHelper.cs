using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Winhance.Infrastructure.Tests.Helpers;

/// <summary>
/// Synthesizes well-formed PNG bytes in-memory for tests that exercise the
/// resolver's BitmapDecoder pipeline. Each pixel is BGRA (matches what the
/// resolver reads with BitmapPixelFormat.Bgra8).
/// </summary>
public static class PngTestHelper
{
    public delegate (byte B, byte G, byte R, byte A) PixelPainter(int x, int y);

    public static async Task<byte[]> MakePngAsync(int width, int height, PixelPainter paint)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var (b, g, r, a) = paint(x, y);
                int i = (y * width + x) * 4;
                pixels[i + 0] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = a;
            }
        }

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            (uint)width,
            (uint)height,
            96.0, 96.0,
            pixels);
        await encoder.FlushAsync();

        stream.Seek(0);
        using var managed = stream.AsStreamForRead();
        using var collector = new MemoryStream();
        await managed.CopyToAsync(collector);
        return collector.ToArray();
    }

    public static Task<byte[]> MakeSolidPngAsync(int width, int height, byte r, byte g, byte b, byte a = 0xFF) =>
        MakePngAsync(width, height, (_, _) => (b, g, r, a));
}
