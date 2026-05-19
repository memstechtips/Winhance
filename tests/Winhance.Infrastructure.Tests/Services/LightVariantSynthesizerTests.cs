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
    public async Task TryGenerateAsync_SolidWhiteOpaque_ReturnsRecoloredBytes()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0xFF, 0xFF, 0xFF);

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().NotBeNull();
        var (r, g, b, a) = await SamplePixelAsync(output!, 0, 0);
        r.Should().Be(0x1F);
        g.Should().Be(0x1F);
        b.Should().Be(0x1F);
        a.Should().Be(0xFF);
    }

    [Fact]
    public async Task TryGenerateAsync_SolidSaturatedGreen_ReturnsNull()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0x10, 0xC0, 0x20);

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_MixedWhiteAndSaturatedAccent_ReturnsNull()
    {
        // 8x8 image: 75% white pixels + 25% saturated red. Mean saturation
        // pulls above MonochromeMaxSaturation (0.15) so detection rejects it.
        var input = await PngTestHelper.MakePngAsync(8, 8, (x, y) =>
            (x < 6) ? ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF)
                    : ((byte)0x00, (byte)0x00, (byte)0xC0, (byte)0xFF));

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_FullyTransparent_ReturnsNull()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0xFF, 0xFF, 0xFF, a: 0x00);

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().BeNull();
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

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().NotBeNull();
        var center = await SamplePixelAsync(output!, 1, 1);
        center.Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0xFF));
        var edge = await SamplePixelAsync(output!, 0, 0);
        edge.Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0x50));
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
