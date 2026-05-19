using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Detects "monochrome-white" PNG icons and produces a darkened companion
/// PNG with all opaque RGB replaced by <see cref="LightVariantTargetColor"/>.
/// Alpha is preserved per-pixel so anti-aliased edges survive intact.
///
/// Caller is <see cref="AppIconResolver"/>'s WriteStreamToCacheAsync, which
/// writes the result alongside the primary cache file as
/// <c>&lt;name&gt;.light.png</c>.
/// </summary>
public static class LightVariantSynthesizer
{
    // Target color for the synthesized variant. Sampled from Win11's own
    // `lightunplated` AppX renders in Settings → Apps (typical range
    // #1A1A1A to #2A2A2A). Looks like a real dark icon, not a washed-out grey.
    // If this changes meaningfully, manually wipe %ProgramData%\Winhance\IconCache.
    private static readonly (byte R, byte G, byte B) LightVariantTargetColor = (0x1F, 0x1F, 0x1F);

    // Same threshold the trim step uses — measure the visible silhouette,
    // ignore antialiasing halo so a soft-edge white icon isn't misclassified
    // by the halo dragging mean saturation around.
    private const byte AlphaDetectionThreshold = 32;

    // Detection thresholds. An icon counts as monochrome-white if the mean
    // HSL lightness of its opaque pixels exceeds MonochromeMinLightness AND
    // the mean saturation falls below MonochromeMaxSaturation. Tuned to catch
    // white vendor marks (GitHub Desktop, etc.) while leaving colored logos
    // and partially-colored marks alone.
    private const double MonochromeMinLightness = 0.85;
    private const double MonochromeMaxSaturation = 0.15;

    /// <summary>
    /// Returns recolored PNG bytes if the input is a monochrome-white icon,
    /// otherwise <c>null</c>. Errors during decode/encode return <c>null</c>
    /// (the resolver treats this as "no variant generated").
    /// </summary>
    public static async Task<byte[]?> TryGenerateAsync(byte[] primaryBytes, CancellationToken ct)
    {
        if (primaryBytes is null || primaryBytes.Length == 0) return null;

        try
        {
            using var inStream = new InMemoryRandomAccessStream();
            await inStream.WriteAsync(primaryBytes.AsBuffer());
            inStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(inStream);

            var sw = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);

            int width = (int)sw.PixelWidth;
            int height = (int)sw.PixelHeight;
            if (width <= 0 || height <= 0) return null;

            var buffer = new Windows.Storage.Streams.Buffer((uint)(width * height * 4));
            sw.CopyToBuffer(buffer);
            var pixels = buffer.ToArray();

            if (!IsMonochromeLight(pixels))
                return null;

            RecolorOpaquePixels(pixels, LightVariantTargetColor);

            sw.CopyFromBuffer(pixels.AsBuffer());

            using var outStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
            encoder.SetSoftwareBitmap(sw);
            await encoder.FlushAsync();

            outStream.Seek(0);
            using var managed = outStream.AsStreamForRead();
            using var collector = new MemoryStream();
            await managed.CopyToAsync(collector, ct).ConfigureAwait(false);
            return collector.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMonochromeLight(byte[] pixels)
    {
        double sumLightness = 0;
        double sumSaturation = 0;
        int opaqueCount = 0;

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte alpha = pixels[i + 3];
            if (alpha <= AlphaDetectionThreshold) continue;

            byte b = pixels[i + 0];
            byte g = pixels[i + 1];
            byte r = pixels[i + 2];
            var (l, s) = RgbToLightnessSaturation(r, g, b);
            sumLightness += l;
            sumSaturation += s;
            opaqueCount++;
        }

        if (opaqueCount == 0) return false;

        double meanLightness = sumLightness / opaqueCount;
        double meanSaturation = sumSaturation / opaqueCount;

        return meanLightness > MonochromeMinLightness
            && meanSaturation < MonochromeMaxSaturation;
    }

    private static void RecolorOpaquePixels(byte[] pixels, (byte R, byte G, byte B) target)
    {
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // alpha > 0 (not the detection threshold): recolor antialiasing
            // halo too, otherwise the dark silhouette ends up rimmed by a
            // faint white glow in light mode.
            if (pixels[i + 3] == 0) continue;

            pixels[i + 0] = target.B;
            pixels[i + 1] = target.G;
            pixels[i + 2] = target.R;
        }
    }

    /// <summary>
    /// Standard HSL conversion (Wikipedia). Returns (lightness, saturation),
    /// each in [0, 1]. Hue isn't needed for the detection so we skip it.
    /// </summary>
    private static (double L, double S) RgbToLightnessSaturation(byte rByte, byte gByte, byte bByte)
    {
        double r = rByte / 255.0;
        double g = gByte / 255.0;
        double b = bByte / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;

        double s;
        if (max == min)
        {
            s = 0;
        }
        else
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        }

        return (l, s);
    }
}
