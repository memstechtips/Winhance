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
        // 8x8 image: 75% white pixels + 25% saturated red. Mean saturation
        // pulls above MonochromeMaxSaturation (0.15) so classification rejects it.
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
