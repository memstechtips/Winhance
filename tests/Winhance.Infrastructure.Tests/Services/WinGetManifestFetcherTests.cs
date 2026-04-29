using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WinGetManifestFetcherTests
{
    private readonly Mock<HttpMessageHandler> _handler = new(MockBehavior.Strict);
    private readonly Mock<ILogService> _mockLog = new();
    private readonly HttpClient _httpClient;
    private readonly WinGetManifestFetcher _fetcher;

    public WinGetManifestFetcherTests()
    {
        _httpClient = new HttpClient(_handler.Object);
        _fetcher = new WinGetManifestFetcher(_httpClient, _mockLog.Object);
    }

    [Fact]
    public async Task GetIconUrlAsync_ReturnsFirstIconUrl_WhenLocaleManifestHasIcons()
    {
        SetupContentsApi("m/Microsoft/PowerToys", new[] { "0.85.0", "0.86.0", "0.87.0" });
        SetupRawFetch("m/Microsoft/PowerToys/0.87.0/Microsoft.PowerToys.locale.en-US.yaml", """
PackageIdentifier: Microsoft.PowerToys
PackageVersion: 0.87.0
PackageLocale: en-US
Icons:
- IconUrl: https://example.com/icons/powertoys-256.png
  IconFileType: png
  IconResolution: square256
- IconUrl: https://example.com/icons/powertoys-128.png
ManifestType: defaultLocale
ManifestVersion: 1.6.0
""");

        var result = await _fetcher.GetIconUrlAsync("Microsoft.PowerToys");
        result.Should().Be("https://example.com/icons/powertoys-256.png");
    }

    [Fact]
    public async Task GetIconUrlAsync_FallsBackToSingletonManifest_OnLocale404()
    {
        SetupContentsApi("o/OldPublisher/OldApp", new[] { "1.0" });
        SetupRawFetch("o/OldPublisher/OldApp/1.0/OldPublisher.OldApp.locale.en-US.yaml",
            statusCode: HttpStatusCode.NotFound);
        SetupRawFetch("o/OldPublisher/OldApp/1.0/OldPublisher.OldApp.yaml", """
PackageIdentifier: OldPublisher.OldApp
PackageVersion: "1.0"
Icons:
- IconUrl: https://example.com/old.png
ManifestType: singleton
ManifestVersion: 1.6.0
""");

        var result = await _fetcher.GetIconUrlAsync("OldPublisher.OldApp");
        result.Should().Be("https://example.com/old.png");
    }

    [Fact]
    public async Task GetIconUrlAsync_ReturnsNull_WhenManifestHasNoIconsBlock()
    {
        SetupContentsApi("g/git/Git", new[] { "2.45.0" });
        SetupRawFetch("g/git/Git/2.45.0/git.Git.locale.en-US.yaml", """
PackageIdentifier: git.Git
PackageVersion: 2.45.0
ManifestType: defaultLocale
ManifestVersion: 1.6.0
""");

        var result = await _fetcher.GetIconUrlAsync("git.Git");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetIconUrlAsync_ConstructsCorrectPath_ForMultiSegmentPackageId()
    {
        // Microsoft.VisualStudio.2022.Community → m/Microsoft/VisualStudio/2022/Community
        SetupContentsApi("m/Microsoft/VisualStudio/2022/Community", new[] { "17.10" });
        SetupRawFetch("m/Microsoft/VisualStudio/2022/Community/17.10/Microsoft.VisualStudio.2022.Community.locale.en-US.yaml", """
Icons:
- IconUrl: https://example.com/vs.png
""");

        var result = await _fetcher.GetIconUrlAsync("Microsoft.VisualStudio.2022.Community");
        result.Should().Be("https://example.com/vs.png");
    }

    [Fact]
    public async Task GetIconUrlAsync_PicksLatestVersion_BySemVer()
    {
        SetupContentsApi("m/Mozilla/Firefox", new[] { "100.0", "9.0", "120.0", "115.0" });
        // Latest by SemVer is 120.0
        SetupRawFetch("m/Mozilla/Firefox/120.0/Mozilla.Firefox.locale.en-US.yaml", """
Icons:
- IconUrl: https://example.com/ff.png
""");

        var result = await _fetcher.GetIconUrlAsync("Mozilla.Firefox");
        result.Should().Be("https://example.com/ff.png");
    }

    [Fact]
    public async Task GetIconUrlAsync_ReturnsNullOnContents404()
    {
        // Package not in winget-pkgs (e.g. a private-source-only package).
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.Host == "api.github.com"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await _fetcher.GetIconUrlAsync("nonexistent.package");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetIconUrlAsync_ThrowsRateLimitException_OnApi403WithZeroRemaining()
    {
        var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.Forbidden);
        rateLimitResponse.Headers.Add("X-RateLimit-Remaining", "0");
        rateLimitResponse.Headers.Add("X-RateLimit-Reset", "1714410000");

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.Host == "api.github.com"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(rateLimitResponse);

        var act = () => _fetcher.GetIconUrlAsync("foo.bar");
        await act.Should().ThrowAsync<WinGetManifestFetcher.RateLimitExceededException>();
    }

    [Fact]
    public async Task GetIconUrlAsync_MemoizesLatestVersion_PerSession()
    {
        SetupContentsApi("m/Mozilla/Firefox", new[] { "120.0" });
        SetupRawFetch("m/Mozilla/Firefox/120.0/Mozilla.Firefox.locale.en-US.yaml", """
Icons:
- IconUrl: https://example.com/ff.png
""");

        await _fetcher.GetIconUrlAsync("Mozilla.Firefox");
        await _fetcher.GetIconUrlAsync("Mozilla.Firefox");

        // Contents API hit only once across two calls; raw fetch may run twice (no result cache,
        // only version cache).
        _handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.Host == "api.github.com"),
            ItExpr.IsAny<CancellationToken>());
    }

    private void SetupContentsApi(string repoPath, string[] versions)
    {
        var json = "[" + string.Join(",",
            Array.ConvertAll(versions, v => $"{{\"name\":\"{v}\",\"type\":\"dir\"}}"))
            + "]";

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri!.Host == "api.github.com"
                    && r.RequestUri.AbsolutePath.EndsWith("/contents/manifests/" + repoPath)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private void SetupRawFetch(string repoPath, string body = "", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri!.Host == "raw.githubusercontent.com"
                    && r.RequestUri.AbsolutePath.EndsWith("/manifests/" + repoPath)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = statusCode == HttpStatusCode.OK
                    ? new StringContent(body, System.Text.Encoding.UTF8, "application/yaml")
                    : new StringContent("")
            });
    }
}
