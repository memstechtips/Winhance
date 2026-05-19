using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Detects monochrome PNG icons and produces theme-appropriate companion PNGs
/// with all opaque RGB replaced by a uniform target color. Alpha is preserved
/// per-pixel so anti-aliased edges survive intact.
///
/// Caller is <see cref="AppIconResolver"/>'s WriteStreamToCacheAsync, which
/// writes the result(s) alongside the primary cache file as
/// <c>&lt;name&gt;.light.png</c> and/or <c>&lt;name&gt;.dark.png</c>:
/// <list type="bullet">
/// <item><description><b>Mono-light source</b> (white-ish vendor marks):
/// only <c>.light.png</c> is generated — the primary already renders correctly
/// in dark mode.</description></item>
/// <item><description><b>Mono-dark source</b> (dark-grey vendor marks like
/// Xbox Game Bar): both <c>.light.png</c> AND <c>.dark.png</c> are generated —
/// the primary's tone (e.g. <c>#333</c>) reads as "faded" against either card
/// background, so both modes need a synthesized variant.</description></item>
/// <item><description><b>Mid-grey or colored source</b>: nothing generated.</description></item>
/// </list>
/// </summary>
public static class LightVariantSynthesizer
{
    // Target colors for the synthesized variants. Light-variant: sampled from
    // Win11's own `lightunplated` AppX renders in Settings → Apps (typical
    // range #1A1A1A to #2A2A2A). Dark-variant: pure white, matching how Win11
    // renders the corresponding `unplated` variant in dark mode.
    // If either changes meaningfully, manually wipe %ProgramData%\Winhance\IconCache.
    private static readonly (byte R, byte G, byte B) LightVariantTargetColor = (0x1F, 0x1F, 0x1F);
    private static readonly (byte R, byte G, byte B) DarkVariantTargetColor = (0xFF, 0xFF, 0xFF);

    // Same threshold the trim step uses — measure the visible silhouette,
    // ignore antialiasing halo so a soft-edge mono icon isn't misclassified
    // by the halo dragging mean saturation around.
    private const byte AlphaDetectionThreshold = 32;

    // Detection thresholds. A "monochrome" icon has mean saturation below
    // MonochromeMaxSaturation (no real color). It is then classified as:
    //   - mono-light if mean lightness > MonochromeMinLightness (e.g. white)
    //   - mono-dark  if mean lightness < MonochromeMaxLightness (e.g. #333)
    //   - mid-grey otherwise (no variants generated; either background works)
    private const double MonochromeMinLightness = 0.85;
    private const double MonochromeMaxLightness = 0.40;
    private const double MonochromeMaxSaturation = 0.15;

    /// <summary>
    /// Returns the synthesized light-mode and dark-mode variants for the
    /// given primary icon bytes. Each may be <c>null</c> independently. Both
    /// <c>null</c> means no variant should be written (colored icon, mid-grey,
    /// or fully-transparent input). Exceptions propagate to the caller —
    /// <see cref="AppIconResolver"/>'s WriteStreamToCacheAsync wraps the call
    /// in its own try/catch with a warning log so a synthesizer failure
    /// produces a debuggable log entry rather than silently dropping both
    /// variants (the previous catch swallowed WinRT errors during the second
    /// encode for mono-dark icons, hiding the root cause).
    /// </summary>
    public static async Task<(byte[]? LightVariant, byte[]? DarkVariant)> TryGenerateAsync(
        byte[] primaryBytes,
        CancellationToken ct)
    {
        if (primaryBytes is null || primaryBytes.Length == 0)
            return (null, null);

        // Decode once. Read pixels into a managed byte[] so each variant can
        // operate on its own private copy — no shared SoftwareBitmap, no
        // shared pixel buffer, no cross-encode lifecycle questions.
        int width;
        int height;
        byte[] sourcePixels;
        {
            using var inStream = new InMemoryRandomAccessStream();
            await inStream.WriteAsync(primaryBytes.AsBuffer());
            inStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(inStream);
            var sw = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);

            width = (int)sw.PixelWidth;
            height = (int)sw.PixelHeight;
            if (width <= 0 || height <= 0) return (null, null);

            var buffer = new Windows.Storage.Streams.Buffer((uint)(width * height * 4));
            sw.CopyToBuffer(buffer);
            sourcePixels = buffer.ToArray();
        }

        var classification = Classify(sourcePixels);
        if (classification == MonochromeClass.NotMonochrome)
            return (null, null);

        // Light-mode variant: any monochrome icon gets a uniform dark recolor
        // for the light card. (Mono-light primary needs this; mono-dark primary
        // needs it too because its source tone reads as faded against light.)
        var lightVariant = await EncodeRecoloredAsync(
            sourcePixels, width, height, LightVariantTargetColor, ct).ConfigureAwait(false);

        // Dark-mode variant: only mono-dark sources need this. Mono-light
        // primary (e.g. plain white) renders correctly against the dark card
        // unchanged, so we skip the extra cache file.
        byte[]? darkVariant = null;
        if (classification == MonochromeClass.MonochromeDark)
        {
            darkVariant = await EncodeRecoloredAsync(
                sourcePixels, width, height, DarkVariantTargetColor, ct).ConfigureAwait(false);
        }

        return (lightVariant, darkVariant);
    }

    private enum MonochromeClass { NotMonochrome, MonochromeLight, MonochromeDark }

    private static MonochromeClass Classify(byte[] pixels)
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

        if (opaqueCount == 0) return MonochromeClass.NotMonochrome;

        double meanLightness = sumLightness / opaqueCount;
        double meanSaturation = sumSaturation / opaqueCount;

        if (meanSaturation >= MonochromeMaxSaturation)
            return MonochromeClass.NotMonochrome;

        if (meanLightness > MonochromeMinLightness)
            return MonochromeClass.MonochromeLight;

        if (meanLightness < MonochromeMaxLightness)
            return MonochromeClass.MonochromeDark;

        // Mid-grey (0.40 ≤ L ≤ 0.85): readable against either background,
        // no synthesized variant adds value.
        return MonochromeClass.NotMonochrome;
    }

    /// <summary>
    /// Produces re-encoded PNG bytes for one variant. Each call gets its own
    /// pixel buffer copy and its own <see cref="SoftwareBitmap"/> — no state
    /// is shared with any other variant produced from the same source. This
    /// avoids the lifecycle hazard we hit before: after the first encoder
    /// flush, reusing the same SoftwareBitmap with a new
    /// <see cref="SoftwareBitmap.CopyFromBuffer"/> would silently fail for
    /// some inputs (notably mono-dark Xbox Game Bar at 111×114 — the second
    /// encode threw inside WinRT and the synthesizer's catch dropped both
    /// variants).
    /// </summary>
    private static async Task<byte[]> EncodeRecoloredAsync(
        byte[] sourcePixels,
        int width,
        int height,
        (byte R, byte G, byte B) target,
        CancellationToken ct)
    {
        var pixels = new byte[sourcePixels.Length];
        Array.Copy(sourcePixels, pixels, sourcePixels.Length);
        RecolorOpaquePixels(pixels, target);

        using var sw = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Straight);
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
