using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class IconManifestServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();

    // Mirrors the strict-handler pattern from AppIconResolverTests:
    // HttpClient.Dispose() calls HttpMessageHandler.Dispose(bool); stub it on Strict mocks.
    private static Mock<HttpMessageHandler> NewStrictHandler()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected().Setup("Dispose", ItExpr.IsAny<bool>());
        return handler;
    }

    private static Mock<HttpMessageHandler> SetupHandler(HttpStatusCode status, string? body = null)
    {
        var handler = NewStrictHandler();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json"),
            });
        return handler;
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_ValidManifest_ReturnsTrueAndPopulatesLookup()
    {
        const string json = """{"icons":{"external/7zip.7zip.png":{"sha256":"abc123"}}}""";
        var handler = SetupHandler(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        var result = await svc.LoadAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_CalledTwiceAfterSuccess_FetchesManifestOnce()
    {
        // The service is a singleton; both startup batches (Windows eager,
        // External background) call LoadAsync. A successful load is cached for
        // the session, so the second call must not hit the network again.
        const string json = """{"icons":{"external/7zip.7zip.png":{"sha256":"abc123"}}}""";
        var handler = SetupHandler(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        (await svc.LoadAsync()).Should().BeTrue();
        (await svc.LoadAsync()).Should().BeTrue();

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_RetriesAfterFailedLoad()
    {
        // A failed load is NOT cached: a transient outage during the first batch
        // must not permanently disable repo icons for the session — the next
        // call re-fetches. First call 404s; second succeeds.
        var calls = 0;
        var handler = NewStrictHandler();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                calls++;
                return calls == 1
                    ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"icons":{"external/7zip.7zip.png":{"sha256":"abc123"}}}"""),
                    };
            });
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        (await svc.LoadAsync()).Should().BeFalse(); // first: 404, not cached
        (await svc.LoadAsync()).Should().BeTrue();  // retried: 200

        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Sha256For_FullRepoPath_ReturnsCorrectHash_AfterLoad()
    {
        const string json = """{"icons":{"external/7zip.7zip.png":{"sha256":"abc123"}}}""";
        var handler = SetupHandler(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);
        await svc.LoadAsync();

        var sha = svc.Sha256For("icons/external/7zip.7zip.png");

        sha.Should().Be("abc123");
    }

    [Fact]
    public async Task Sha256For_AbsentPath_ReturnsNull_AfterLoad()
    {
        const string json = """{"icons":{"external/7zip.7zip.png":{"sha256":"abc123"}}}""";
        var handler = SetupHandler(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);
        await svc.LoadAsync();

        var sha = svc.Sha256For("icons/external/missing.png");

        sha.Should().BeNull();
    }

    [Fact]
    public async Task Sha256For_PathWithoutIconsPrefix_StillResolves()
    {
        const string json = """{"icons":{"external/7zip.7zip.png":{"sha256":"abc123"}}}""";
        var handler = SetupHandler(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);
        await svc.LoadAsync();

        // Path already stripped of "icons/" — still resolves via direct key match.
        var sha = svc.Sha256For("external/7zip.7zip.png");

        sha.Should().Be("abc123");
    }

    // ── HTTP failure ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_Http404_ReturnsFalse()
    {
        var handler = SetupHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        var result = await svc.LoadAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Sha256For_ReturnsNull_WhenNotLoaded()
    {
        var handler = SetupHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);
        await svc.LoadAsync(); // fails

        var sha = svc.Sha256For("icons/external/7zip.7zip.png");

        sha.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_Http404_LogsWarning()
    {
        var handler = SetupHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        await svc.LoadAsync();

        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("404"))), Times.Once);
    }

    // ── Malformed JSON ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_MalformedJson_ReturnsFalseWithoutThrowing()
    {
        var handler = SetupHandler(HttpStatusCode.OK, "not-json-at-all{{{{");
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        var act = async () => await svc.LoadAsync();

        await act.Should().NotThrowAsync();
        (await svc.LoadAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_MissingIconsProperty_ReturnsFalse()
    {
        var handler = SetupHandler(HttpStatusCode.OK, """{"other":{"key":"value"}}""");
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        var result = await svc.LoadAsync();

        result.Should().BeFalse();
    }

    // ── Network exception ────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_NetworkException_ReturnsFalseWithoutThrowing()
    {
        var handler = NewStrictHandler();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network unreachable"));
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        var act = async () => await svc.LoadAsync();

        await act.Should().NotThrowAsync();
        var result = await svc.LoadAsync();
        result.Should().BeFalse();
    }

    // ── Logging on success ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_Success_LogsInformationWithCount()
    {
        const string json = """{"icons":{"external/7zip.7zip.png":{"sha256":"abc123"},"windows/calc.png":{"sha256":"def456"}}}""";
        var handler = SetupHandler(HttpStatusCode.OK, json);
        using var client = new HttpClient(handler.Object);
        var svc = new IconManifestService(client, _mockLog.Object);

        await svc.LoadAsync();

        _mockLog.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("2"))), Times.Once);
    }
}
