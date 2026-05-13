using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Windows.Foundation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class BinaryIconSourceTests
{
    private readonly Mock<IShellImageFactory> _mockFactory = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly BinaryIconSource _source;

    public BinaryIconSourceTests()
    {
        _source = new BinaryIconSource(_mockFactory.Object, _mockLog.Object);
    }

    [Fact]
    public async Task GetIconStreamAsync_ReturnsNull_WhenPathIsNullOrEmpty()
    {
        (await _source.GetIconStreamAsync("", new Size(96, 96))).Should().BeNull();
        (await _source.GetIconStreamAsync("   ", new Size(96, 96))).Should().BeNull();

        _mockFactory.Verify(
            f => f.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetIconStreamAsync_ReturnsStream_OnFactorySuccess()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // "PNG" header bytes (fake)
        _mockFactory.Setup(f => f.GetIconBytesAsync("C:\\PowerToys\\PowerToys.exe", It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var result = await _source.GetIconStreamAsync("C:\\PowerToys\\PowerToys.exe", new Size(96, 96));

        result.Should().NotBeNull();
        using var ms = new MemoryStream();
        await result!.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task GetIconStreamAsync_ReturnsNull_AndLogsWarning_OnFactoryException()
    {
        _mockFactory.Setup(f => f.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("path not found"));

        var result = await _source.GetIconStreamAsync("C:\\Bogus\\path.exe", new Size(96, 96));

        result.Should().BeNull();
        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("path not found"))), Times.Once);
    }

    [Fact]
    public async Task GetIconStreamAsync_ReturnsNull_OnEmptyByteArray()
    {
        // Treat empty bytes as a missing icon, not a successful empty stream.
        _mockFactory.Setup(f => f.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<byte>());

        (await _source.GetIconStreamAsync("C:\\anything.exe", new Size(96, 96))).Should().BeNull();
    }

    [Fact]
    public async Task GetIconStreamAsync_PropagatesExternalCancellation()
    {
        // External cancellation (caller's ct fires) re-throws — see the
        // "catch (OperationCanceledException) when (ct.IsCancellationRequested) throw"
        // arm in BinaryIconSource. Distinct from internal-timeout cancellation,
        // which collapses to null.
        _mockFactory.Setup(f => f.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .Returns<string, Size, CancellationToken>(async (_, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
                return Array.Empty<byte>();
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var act = () => _source.GetIconStreamAsync("C:\\anything.exe", new Size(96, 96), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
