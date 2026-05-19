using System.Runtime.InteropServices.WindowsRuntime;
using FluentAssertions;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace Winhance.Infrastructure.Tests.Helpers;

public class PngTestHelperTests
{
    [Fact]
    public async Task MakeSolidPngAsync_ProducesDecodablePngWithExpectedPixels()
    {
        var bytes = await PngTestHelper.MakeSolidPngAsync(4, 4, 0xFF, 0xFF, 0xFF);

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        decoder.PixelWidth.Should().Be(4);
        decoder.PixelHeight.Should().Be(4);

        var sw = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
        var buffer = new Windows.Storage.Streams.Buffer((uint)(sw.PixelWidth * sw.PixelHeight * 4));
        sw.CopyToBuffer(buffer);
        var pixels = buffer.ToArray();

        pixels[0].Should().Be(0xFF);
        pixels[1].Should().Be(0xFF);
        pixels[2].Should().Be(0xFF);
        pixels[3].Should().Be(0xFF);
    }
}
