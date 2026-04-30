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
    private readonly Mock<HttpMessageHandler> _handler = new(MockBehavior.Strict);
    private readonly Mock<ILogService> _mockLog = new();
    private readonly HttpClient _httpClient;

    public WinGetIconSourceTests()
    {
        _httpClient = new HttpClient(_handler.Object);
    }

    /// <summary>
    /// Builds a fresh source per test with explicit fakes for the COM path and
    /// the override-map lookup. Per-test instantiation avoids capturing-delegate
    /// problems and keeps each test self-contained.
    /// </summary>
    private WinGetIconSource BuildSource(
        Func<string, CancellationToken, Task<string?>>? com = null,
        Func<string, CancellationToken, Task<string?>>? overrideLookup = null)
    {
        return new WinGetIconSource(
            _mockBootstrapper.Object,
            _httpClient,
            _mockLog.Object,
            comIconUrlsAsync: com ?? ((_, _) => Task.FromResult<string?>(null)),
            overrideLookup: overrideLookup ?? ((_, _) => Task.FromResult<string?>(null)));
    }

    [Fact]
    public async Task ReturnsStream_FromCom_WhenComYieldsUrl()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(true);
        SetupIconDownload("https://example.com/com.png", new byte[] { 1, 2, 3 });

        // Override would also have a URL, but COM should win and the override should not be consulted.
        bool overrideConsulted = false;
        var source = BuildSource(
            com: (_, _) => Task.FromResult<string?>("https://example.com/com.png"),
            overrideLookup: (_, _) => { overrideConsulted = true; return Task.FromResult<string?>("https://example.com/should-not-be-used.png"); });

        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().NotBeNull();
        overrideConsulted.Should().BeFalse("COM provided a URL, override map must not be consulted");
    }

    [Fact]
    public async Task ReturnsNull_WhenComReturnsCleanMiss_DoesNotConsultOverride()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(true);

        // COM returns null cleanly (manifest exists, no Icons block) — that's authoritative
        // for this package. The override map is for cases where COM couldn't tell us, not
        // for cases where COM told us "no icon."
        bool overrideConsulted = false;
        var source = BuildSource(
            com: (_, _) => Task.FromResult<string?>(null),
            overrideLookup: (_, _) => { overrideConsulted = true; return Task.FromResult<string?>("https://example.com/should-not-be-used.png"); });

        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().BeNull();
        overrideConsulted.Should().BeFalse("COM clean miss is authoritative; override is reserved for COM failure / unavailability");
    }

    [Fact]
    public async Task ReturnsStream_FromOverride_WhenComThrows()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(true);
        SetupIconDownload("https://example.com/override.png", new byte[] { 9 });

        var source = BuildSource(
            com: (_, _) => Task.FromException<string?>(new InvalidOperationException("COM glitch")),
            overrideLookup: (id, _) => Task.FromResult<string?>(id == "Some.Package" ? "https://example.com/override.png" : null));

        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenComThrows_AndOverrideMisses()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(true);

        var source = BuildSource(
            com: (_, _) => Task.FromException<string?>(new InvalidOperationException("COM glitch")),
            overrideLookup: (_, _) => Task.FromResult<string?>(null));

        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SkipsCom_WhenSystemWinGetUnavailable_AndUsesOverride()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(false);
        SetupIconDownload("https://example.com/override.png", new byte[] { 7 });

        // COM func should never be invoked when bootstrap reports unavailable — make it throw if called.
        var source = BuildSource(
            com: (_, _) => throw new InvalidOperationException("should not be called"),
            overrideLookup: (_, _) => Task.FromResult<string?>("https://example.com/override.png"));

        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SkipsCom_WhenSystemWinGetUnavailable_AndOverrideMisses_ReturnsNull()
    {
        _mockBootstrapper.SetupGet(b => b.IsSystemWinGetAvailable).Returns(false);

        var source = BuildSource(
            com: (_, _) => throw new InvalidOperationException("should not be called"),
            overrideLookup: (_, _) => Task.FromResult<string?>(null));

        var result = await source.GetIconStreamAsync("Some.Package");

        result.Should().BeNull();
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
