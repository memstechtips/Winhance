using System.Runtime.InteropServices.WindowsRuntime;
using FluentAssertions;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Tests.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class LightVariantSynthesizerTests
{
    [Fact]
    public async Task TryGenerateAsync_SolidWhiteOpaque_WritesLightVariantOnly()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0xFF, 0xFF, 0xFF);

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().NotBeNull("mono-light source needs a dark recolor for light mode");
        dark.Should().BeNull("mono-light source already renders correctly in dark mode unchanged");

        var (r, g, b, a) = await SamplePixelAsync(light!, 0, 0);
        r.Should().Be(0x1F);
        g.Should().Be(0x1F);
        b.Should().Be(0x1F);
        a.Should().Be(0xFF);
    }

    [Fact]
    public async Task TryGenerateAsync_SolidDarkGreyOpaque_WritesBothVariants()
    {
        // RGB (0x33,0x33,0x33) — matches the real Xbox Game Bar cache file.
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0x33, 0x33, 0x33);

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().NotBeNull("mono-dark source needs a uniform #1F1F1F recolor for light mode");
        dark.Should().NotBeNull("mono-dark source needs a white recolor for dark mode");

        var lightCenter = await SamplePixelAsync(light!, 0, 0);
        lightCenter.Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0xFF));

        var darkCenter = await SamplePixelAsync(dark!, 0, 0);
        darkCenter.Should().Be(((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF));
    }

    [Fact]
    public async Task TryGenerateAsync_SolidSaturatedGreen_ReturnsNoVariants()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0x10, 0xC0, 0x20);

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().BeNull();
        dark.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_StickyNotesLikeIcon_ReturnsNoVariants()
    {
        // Regression for the Sticky Notes case: a vivid yellow shape sitting
        // on a much larger dark-grey background. Mean saturation across the
        // whole image is well below 0.15 (the grey area dilutes it), but the
        // yellow pixels are unmistakably colored and the icon must not be
        // recolored. The count-based check catches this where the mean check
        // didn't.
        //
        // 20×20 image: ~20% of opaque pixels are saturated yellow, the rest
        // are dark grey. Total opaque saturation mean ≈ 0.10 (below the old
        // 0.15 mean threshold), but the yellow count is way above 5% of opaque.
        var input = await PngTestHelper.MakePngAsync(20, 20, (x, y) =>
        {
            bool isYellowShape = x >= 8 && x < 16 && y >= 8 && y < 12;
            return isYellowShape
                ? ((byte)0x00, (byte)0xE0, (byte)0xE0, (byte)0xFF)   // BGR: yellow (R=0xE0, G=0xE0, B=0x00)
                : ((byte)0x40, (byte)0x40, (byte)0x40, (byte)0xFF);  // dark grey
        });

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().BeNull("colored content must not be recolored to monochrome");
        dark.Should().BeNull("colored content must not be recolored to monochrome");
    }

    [Fact]
    public async Task TryGenerateAsync_MonoWithTrivialColorNoise_StillClassifiedAsMonochrome()
    {
        // Two pixels out of 64 carry a vivid red. 2/64 = 3.1% — under the
        // 5% colored-pixel tolerance — so the icon should still classify as
        // mono-light and produce a .light.png. Guards against the count-based
        // detection being too sensitive on icons with a few stray noise pixels
        // (rounding artifacts in source PNGs, JPEG compression bleed-through).
        var input = await PngTestHelper.MakePngAsync(8, 8, (x, y) =>
        {
            if ((x == 0 && y == 0) || (x == 7 && y == 7))
                return ((byte)0x00, (byte)0x00, (byte)0xFF, (byte)0xFF); // BGR: red noise
            return ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF);     // white
        });

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().NotBeNull("two noise pixels (3% of 64) should not disqualify a mono-light icon");
        dark.Should().BeNull("mono-light primary already works in dark mode");
    }

    [Fact]
    public async Task TryGenerateAsync_TwoToneMonochromeIcon_ReturnsNoVariants()
    {
        // Regression for Windows Terminal: a pure-monochrome icon (S=0) that
        // is composed of a dark region AND a light region — a dark window
        // with a light `>_` glyph. mean lightness lands in the mono-dark
        // band, but flattening every opaque pixel to one tone would collapse
        // it into a solid square. The two-tone guard must reject it.
        //
        // 10×10: columns 0-6 dark #202020 (70%), columns 7-9 light #E0E0E0
        // (30%). Both bands far exceed the 10% TwoToneMinBandFraction.
        var input = await PngTestHelper.MakePngAsync(10, 10, (x, y) =>
            x < 7
                ? ((byte)0x20, (byte)0x20, (byte)0x20, (byte)0xFF)   // dark
                : ((byte)0xE0, (byte)0xE0, (byte)0xE0, (byte)0xFF)); // light

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().BeNull("a composed dark+light icon must not be flattened");
        dark.Should().BeNull("a composed dark+light icon must not be flattened");
    }

    [Fact]
    public async Task TryGenerateAsync_DarkIconWithMinorLightHighlights_StillClassifiedAsMonoDark()
    {
        // Guards against the two-tone check being too eager: a dark
        // silhouette with a few light highlight pixels (under the 10% band
        // fraction) is still a recolorable mono-dark icon. 10×10 = 100 px,
        // 6 light highlight pixels = 6% — below TwoToneMinBandFraction.
        var input = await PngTestHelper.MakePngAsync(10, 10, (x, y) =>
        {
            bool isHighlight = y == 0 && x < 6;
            return isHighlight
                ? ((byte)0xF0, (byte)0xF0, (byte)0xF0, (byte)0xFF)   // light highlight
                : ((byte)0x33, (byte)0x33, (byte)0x33, (byte)0xFF);  // dark body
        });

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().NotBeNull("6% light highlights should not trip the two-tone guard");
        dark.Should().NotBeNull("mono-dark icon still needs a dark-mode variant");
    }

    [Fact]
    public async Task TryGenerateAsync_MidGreyMonochrome_ReturnsNoVariants()
    {
        // Mean lightness ~0.5 (#808080) — sits in the dead zone between
        // MonochromeMaxLightness (0.40) and MonochromeMinLightness (0.85).
        // The source already reads on either background; no synthesized
        // variant adds value.
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0x80, 0x80, 0x80);

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().BeNull();
        dark.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_MixedWhiteAndSaturatedAccent_ReturnsNoVariants()
    {
        // 8x8 image: 75% white pixels + 25% saturated red. 25% of opaque
        // pixels carry saturation > 0.20, well above the 5% colored-pixel
        // fraction, so classification rejects it as NotMonochrome.
        var input = await PngTestHelper.MakePngAsync(8, 8, (x, y) =>
            (x < 6) ? ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF)
                    : ((byte)0x00, (byte)0x00, (byte)0xC0, (byte)0xFF));

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().BeNull();
        dark.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_FullyTransparent_ReturnsNoVariants()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0xFF, 0xFF, 0xFF, a: 0x00);

        var (light, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().BeNull();
        dark.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_WhiteWithAntialiasedEdge_PreservesAlphaInRecolor()
    {
        // 4x4 image: opaque white center pixel (1,1), feathered edge pixel
        // (0,0) at alpha=0x50. After recolor: (1,1) should be #1F1F1F/0xFF,
        // (0,0) should be #1F1F1F/0x50 — RGB replaced, alpha preserved.
        var input = await PngTestHelper.MakePngAsync(4, 4, (x, y) =>
        {
            if (x == 1 && y == 1) return ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF);
            if (x == 0 && y == 0) return ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0x50);
            return ((byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00);
        });

        var (light, _) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        light.Should().NotBeNull();
        var center = await SamplePixelAsync(light!, 1, 1);
        center.Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0xFF));
        var edge = await SamplePixelAsync(light!, 0, 0);
        edge.Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0x50));
    }

    [Fact]
    public async Task TryGenerateAsync_DarkGreyWithAntialiasedEdge_PreservesAlphaInDarkRecolor()
    {
        // Mirror of the previous test for the mono-dark path: the dark
        // variant recolors to white but must preserve alpha on the
        // feathered edge pixel, otherwise the silhouette grows a hard halo.
        var input = await PngTestHelper.MakePngAsync(4, 4, (x, y) =>
        {
            if (x == 1 && y == 1) return ((byte)0x33, (byte)0x33, (byte)0x33, (byte)0xFF);
            if (x == 0 && y == 0) return ((byte)0x33, (byte)0x33, (byte)0x33, (byte)0x50);
            return ((byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00);
        });

        var (_, dark) = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        dark.Should().NotBeNull();
        var center = await SamplePixelAsync(dark!, 1, 1);
        center.Should().Be(((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF));
        var edge = await SamplePixelAsync(dark!, 0, 0);
        edge.Should().Be(((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0x50));
    }

    private static async Task<(byte R, byte G, byte B, byte A)> SamplePixelAsync(byte[] pngBytes, int x, int y)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var sw = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
        var buffer = new Windows.Storage.Streams.Buffer((uint)(sw.PixelWidth * sw.PixelHeight * 4));
        sw.CopyToBuffer(buffer);
        var pixels = buffer.ToArray();
        int i = (y * (int)sw.PixelWidth + x) * 4;
        return (pixels[i + 2], pixels[i + 1], pixels[i + 0], pixels[i + 3]);
    }
}
