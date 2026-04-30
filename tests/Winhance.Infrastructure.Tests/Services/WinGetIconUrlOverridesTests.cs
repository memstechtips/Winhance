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

public class WinGetIconUrlOverridesTests
{
    private const string TestIndexUrl = "https://test.invalid/screenshot-database-v2.json";

    private readonly Mock<HttpMessageHandler> _handler = new(MockBehavior.Strict);
    private readonly Mock<ILogService> _mockLog = new();
    private readonly HttpClient _httpClient;

    public WinGetIconUrlOverridesTests()
    {
        _httpClient = new HttpClient(_handler.Object);
    }

    private WinGetIconUrlOverrides Build() =>
        new(_httpClient, _mockLog.Object, TestIndexUrl);

    [Fact]
    public async Task ReturnsUrl_WhenDatabaseHasMatchingEntry()
    {
        SetupResponse(HttpStatusCode.OK, """
            {
              "package_count": { "total": 2, "done": 2 },
              "icons_and_screenshots": {
                "mozilla.firefox": { "icon": "https://i.postimg.cc/firefox.png", "images": [] },
                "brave.brave":     { "icon": "https://i.postimg.cc/brave.png",   "images": [] }
              }
            }
            """);

        var overrides = Build();
        var url = await overrides.TryGetAsync("mozilla.firefox");

        url.Should().Be("https://i.postimg.cc/firefox.png");
    }

    [Fact]
    public async Task LookupIsCaseInsensitive_AgainstDatabaseKey()
    {
        // UniGetUI's convention is lowercased keys; we look up case-insensitively
        // so callers can pass the canonical mixed-case WinGet ID.
        SetupResponse(HttpStatusCode.OK, """
            {
              "icons_and_screenshots": {
                "mozilla.firefox": { "icon": "https://i.postimg.cc/firefox.png" }
              }
            }
            """);

        var overrides = Build();
        var url = await overrides.TryGetAsync("Mozilla.Firefox");

        url.Should().Be("https://i.postimg.cc/firefox.png");
    }

    [Fact]
    public async Task ReturnsNull_WhenDatabaseHasNoMatchingEntry()
    {
        SetupResponse(HttpStatusCode.OK, """
            {
              "icons_and_screenshots": {
                "mozilla.firefox": { "icon": "https://i.postimg.cc/firefox.png" }
              }
            }
            """);

        var overrides = Build();
        var url = await overrides.TryGetAsync("Some.OtherPackage");

        url.Should().BeNull();
    }

    [Fact]
    public async Task SkipsEntries_WithoutIconField()
    {
        // Some upstream entries may have screenshots but no curated icon yet —
        // those should be skipped rather than crashing the load.
        SetupResponse(HttpStatusCode.OK, """
            {
              "icons_and_screenshots": {
                "with.icon":    { "icon": "https://i.postimg.cc/with-icon.png" },
                "without.icon": { "images": ["https://i.postimg.cc/screenshot.png"] }
              }
            }
            """);

        var overrides = Build();
        (await overrides.TryGetAsync("with.icon")).Should().Be("https://i.postimg.cc/with-icon.png");
        (await overrides.TryGetAsync("without.icon")).Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNull_AndLogsWarning_WhenFetchFails()
    {
        SetupResponse(HttpStatusCode.NotFound, "");

        var overrides = Build();
        var url = await overrides.TryGetAsync("mozilla.firefox");

        url.Should().BeNull();
        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("404"))), Times.Once);
    }

    [Fact]
    public async Task ReturnsNull_WhenIconsAndScreenshotsMissing()
    {
        SetupResponse(HttpStatusCode.OK, """
            { "package_count": { "total": 0 } }
            """);

        var overrides = Build();
        var url = await overrides.TryGetAsync("mozilla.firefox");

        url.Should().BeNull();
        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("icons_and_screenshots"))), Times.Once);
    }

    [Fact]
    public async Task ReturnsNull_WhenJsonMalformed()
    {
        SetupResponse(HttpStatusCode.OK, "{ not valid json");

        var overrides = Build();
        var url = await overrides.TryGetAsync("mozilla.firefox");

        url.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNull_ForEmptyOrWhitespacePackageId()
    {
        // No HTTP setup — the strict handler would fail if a request was made.
        var overrides = Build();
        (await overrides.TryGetAsync("")).Should().BeNull();
        (await overrides.TryGetAsync("   ")).Should().BeNull();
    }

    [Fact]
    public async Task FetchesOnlyOnce_AcrossConcurrentLookups()
    {
        int requestCount = 0;
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref requestCount);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                          "icons_and_screenshots": {
                            "mozilla.firefox": { "icon": "https://i.postimg.cc/firefox.png" }
                          }
                        }
                        """),
                };
            });

        var overrides = Build();

        var tasks = new[]
        {
            overrides.TryGetAsync("mozilla.firefox"),
            overrides.TryGetAsync("brave.brave"),
            overrides.TryGetAsync("mozilla.firefox"),
        };
        await Task.WhenAll(tasks);

        // All three lookups share the single fetch (Lazy<Task> caches the load).
        requestCount.Should().Be(1);
    }

    private void SetupResponse(HttpStatusCode status, string body)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body),
            });
    }
}
