using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WinGetIconSourceTests
{
    private readonly Mock<IWinGetBootstrapper> _mockBootstrapper = new();
    private readonly Mock<IWinGetManifestFetcher> _mockFetcher = new();
    private readonly Mock<HttpMessageHandler> _handler = new(MockBehavior.Strict);
    private readonly Mock<ILogService> _mockLog = new();
    private readonly HttpClient _httpClient;

    public WinGetIconSourceTests()
    {
        _httpClient = new HttpClient(_handler.Object);
    }

    /// <summary>
    /// Builds a fresh source per test with a specific COM-fake function. Per-test
    /// instantiation avoids capturing-delegate problems and keeps each test self-contained.
    /// </summary>
    private WinGetIconSource BuildSource(Func<string, CancellationToken, Task<string?>>? com = null)
    {
        return new WinGetIconSource(
            _mockBootstrapper.Object,
            _mockFetcher.Object,
            _httpClient,
            _mockLog.Object,
            comIconUrlsAsync: com ?? ((_, _) => Task.FromResult<string?>(null)));
    }

    [Fact]
    public async Task ReturnsStream_FromCom_WhenComYieldsUrl()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(true);
        SetupIconDownload("https://example.com/com.png", new byte[] { 1, 2, 3 });

        var source = BuildSource(com: (_, _) => Task.FromResult<string?>("https://example.com/com.png"));
        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().NotBeNull();
        _mockFetcher.Verify(f => f.GetIconUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReturnsStream_FromManifestFetcher_WhenComThrows()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(true);
        _mockFetcher.Setup(f => f.GetIconUrlAsync("Some.Package", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/manifest.png");
        SetupIconDownload("https://example.com/manifest.png", new byte[] { 9 });

        var source = BuildSource(com: (_, _) => Task.FromException<string?>(new InvalidOperationException("COM glitch")));
        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenComReturnsCleanMiss()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(true);

        // COM returns null (no Icons in manifest) — clean miss, fetcher is NOT consulted.
        var source = BuildSource(com: (_, _) => Task.FromResult<string?>(null));
        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().BeNull();
        _mockFetcher.Verify(f => f.GetIconUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SkipsCom_WhenSystemWinGetUnavailable()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockFetcher.Setup(f => f.GetIconUrlAsync("Some.Package", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/manifest.png");
        SetupIconDownload("https://example.com/manifest.png", new byte[] { 9 });

        // COM func should never be invoked when bootstrap reports unavailable — make it throw if called.
        var source = BuildSource(com: (_, _) => throw new InvalidOperationException("should not be called"));
        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().NotBeNull();
        _mockFetcher.Verify(f => f.GetIconUrlAsync("Some.Package", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShortCircuitsLayer_AfterRateLimitException()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(false);
        _mockFetcher.SetupSequence(f => f.GetIconUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WinGetManifestFetcher.RateLimitExceededException(new HttpResponseMessage().Headers))
            .ReturnsAsync("https://example.com/should-not-be-called.png");

        var source = BuildSource();
        var first = await source.GetIconStreamAsync("Pkg.One");
        var second = await source.GetIconStreamAsync("Pkg.Two");

        first.Should().BeNull();
        second.Should().BeNull();
        // Fetcher hit only once — second call short-circuits before the fetcher.
        _mockFetcher.Verify(f => f.GetIconUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("rate limit"))), Times.Once);
    }

    private void SetupIconDownload(string url, byte[] bytes)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsoluteUri == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            });
    }
}
