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
    public async Task ResolvesByNormalizedIconId_DroppingPublisherPrefix()
    {
        // Real-world case: UniGetUI's database keys WinGet packages by the
        // app-name half (after the publisher dot), lowercased and with
        // separators normalized. So `Mozilla.Firefox` → `firefox`.
        SetupResponse(HttpStatusCode.OK, """
            {
              "package_count": { "total": 2, "done": 2 },
              "icons_and_screenshots": {
                "firefox": { "icon": "https://i.postimg.cc/firefox.png", "images": [] },
                "brave":   { "icon": "https://i.postimg.cc/brave.png",   "images": [] }
              }
            }
            """);

        var overrides = Build();
        (await overrides.TryGetAsync("Mozilla.Firefox")).Should().Be("https://i.postimg.cc/firefox.png");
        (await overrides.TryGetAsync("Brave.Brave")).Should().Be("https://i.postimg.cc/brave.png");
    }

    [Fact]
    public async Task ResolvesByFullId_WhenDatabaseHappensToHaveOne()
    {
        // Some upstream entries are keyed by the full WinGet ID (rare but possible).
        // Our lookup tries the full ID before the normalized icon ID.
        SetupResponse(HttpStatusCode.OK, """
            {
              "icons_and_screenshots": {
                "Mozilla.Firefox": { "icon": "https://i.postimg.cc/full-id.png" }
              }
            }
            """);

        var overrides = Build();
        var url = await overrides.TryGetAsync("Mozilla.Firefox");

        url.Should().Be("https://i.postimg.cc/full-id.png");
    }

    [Fact]
    public async Task ResolvesByManagerPrefixedId_WhenDatabaseHasOne()
    {
        // UniGetUI's most-specific key shape: "Winget.<full id>". Our lookup
        // tries this first per UniGetUI's own ordering.
        SetupResponse(HttpStatusCode.OK, """
            {
              "icons_and_screenshots": {
                "Winget.Mozilla.Firefox": { "icon": "https://i.postimg.cc/manager-prefixed.png" }
              }
            }
            """);

        var overrides = Build();
        var url = await overrides.TryGetAsync("Mozilla.Firefox");

        url.Should().Be("https://i.postimg.cc/manager-prefixed.png");
    }

    [Fact]
    public async Task LookupIsCaseInsensitive()
    {
        SetupResponse(HttpStatusCode.OK, """
            { "icons_and_screenshots": { "firefox": { "icon": "https://i.postimg.cc/firefox.png" } } }
            """);

        var overrides = Build();
        // Database key is lowercase, callers pass mixed-case canonical IDs.
        (await overrides.TryGetAsync("Mozilla.Firefox")).Should().Be("https://i.postimg.cc/firefox.png");
        (await overrides.TryGetAsync("MOZILLA.FIREFOX")).Should().Be("https://i.postimg.cc/firefox.png");
    }

    [Fact]
    public async Task NormalizesMultiDotIds_WithHyphens()
    {
        // Multi-dot IDs: drop publisher only, then replace remaining dots with hyphens.
        // E.g. Microsoft.PowerToys.Preview → "PowerToys.Preview" → "PowerToys-Preview".
        SetupResponse(HttpStatusCode.OK, """
            { "icons_and_screenshots": { "powertoys-preview": { "icon": "https://i.postimg.cc/pt.png" } } }
            """);

        var overrides = Build();
        var url = await overrides.TryGetAsync("Microsoft.PowerToys.Preview");

        url.Should().Be("https://i.postimg.cc/pt.png");
    }

    [Fact]
    public async Task ReturnsNull_ForSingleTokenIdsWithNoDot()
    {
        // Edge case: ID with no dot has no publisher to drop, so the
        // normalized form would be the whole ID. We don't try that path
        // (UniGetUI doesn't either); just the full-ID and Winget-prefixed
        // lookups, both of which miss for these unusual cases.
        SetupResponse(HttpStatusCode.OK, """
            { "icons_and_screenshots": { "lone": { "icon": "https://i.postimg.cc/lone.png" } } }
            """);

        var overrides = Build();
        var url = await overrides.TryGetAsync("lone");

        url.Should().BeNull();
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
