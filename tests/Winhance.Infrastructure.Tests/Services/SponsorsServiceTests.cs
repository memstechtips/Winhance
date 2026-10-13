using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SponsorsServiceTests
{
    private const string LocalAppData = @"C:\Users\Test\AppData\Local";
    private const string LiveJson = """{"updated":"live"}""";
    private const string LiveBase = "https://raw.githubusercontent.com/memstechtips/Winhance/sponsors/sponsors/";
    private const string ListWithLogo = """{"sponsors":[{"id":"acme","name":"Acme","tier":"gold","logo":"logos/acme.png"}]}""";

    private static readonly int[] RetryWaits = [500, 1000];
    private static readonly string LastGoodFolder = Path.Combine(LocalAppData, "Winhance", "Cache");
    private static readonly string LastGoodPath = Path.Combine(LastGoodFolder, "sponsors.json");
    private static readonly string BundledPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sponsors", "sponsors.json");
    private static readonly string CachedLogoPath = Path.Combine(LastGoodFolder, "logos", "acme.png");
    private static readonly byte[] LogoBytes = [1, 2, 3];

    private readonly Mock<HttpMessageHandler> _handler = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly List<int> _waits = [];
    private readonly ManualTimeProvider _clock = new();
    private readonly SponsorsService _sut;

    public SponsorsServiceTests()
    {
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => Path.Combine(parts));

        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser
            .Setup(u => u.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(LocalAppData);

        _sut = new SponsorsService(
            new HttpClient(_handler.Object),
            Mock.Of<ILogService>(),
            _files.Object,
            interactiveUser.Object,
            _clock,
            (ms, _) =>
            {
                _waits.Add(ms);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task GetSponsorsAsync_TwoFailedFetches_RetriesAfterShortWaitsAndReturnsTheLiveList()
    {
        LiveAnswers(() => throw new HttpRequestException("Connection reset"), Unavailable, Ok);

        var sponsors = await _sut.GetSponsorsAsync();

        sponsors!.Updated.Should().Be("live");
        _waits.Should().Equal(RetryWaits);
    }

    [Fact]
    public async Task GetSponsorsAsync_LiveFetchSucceeds_SavesItAsTheLastGoodFetch()
    {
        LiveAnswers(Ok);

        await _sut.GetSponsorsAsync();

        _files.Verify(f => f.CreateDirectory(LastGoodFolder), Times.Once);
        _files.Verify(f => f.WriteAllTextAsync(LastGoodPath, LiveJson, It.IsAny<CancellationToken>()), Times.Once);
        _waits.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSponsorsAsync_EveryFetchFails_PrefersTheLastGoodFetchOverTheBundledSnapshot()
    {
        LiveAnswers(Unavailable, Unavailable, Unavailable);
        OnDisk(LastGoodPath, """{"updated":"last good"}""");
        OnDisk(BundledPath, """{"updated":"bundled"}""");

        var sponsors = await _sut.GetSponsorsAsync();

        sponsors!.Updated.Should().Be("last good");
        _handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(3),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSponsorsAsync_EveryFetchFailsAndNoneWasSaved_FallsBackToTheBundledSnapshot()
    {
        LiveAnswers(Unavailable, Unavailable, Unavailable);
        OnDisk(BundledPath, """{"updated":"bundled"}""");

        var sponsors = await _sut.GetSponsorsAsync();

        sponsors!.Updated.Should().Be("bundled");
    }

    [Fact]
    public async Task GetSnapshotAsync_NeverTouchesTheNetwork_AndPrefersTheLastGoodFetch()
    {
        OnDisk(LastGoodPath, """{"updated":"last good"}""");
        OnDisk(BundledPath, """{"updated":"bundled"}""");

        var sponsors = await _sut.GetSnapshotAsync();

        sponsors!.Updated.Should().Be("last good");
        _handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetSponsorsAsync_FetchTimesOut_DoesNotRetry()
    {
        LiveAnswers(() => throw new TaskCanceledException("The request timed out."));
        OnDisk(BundledPath, """{"updated":"bundled"}""");

        var sponsors = await _sut.GetSponsorsAsync();

        sponsors!.Updated.Should().Be("bundled");
        _waits.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSponsorsAsync_ClientError_DoesNotRetry()
    {
        LiveAnswers(() => new HttpResponseMessage(HttpStatusCode.NotFound));
        OnDisk(BundledPath, """{"updated":"bundled"}""");

        var sponsors = await _sut.GetSponsorsAsync();

        sponsors!.Updated.Should().Be("bundled");
        _waits.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSponsorsAsync_LiveListYoungerThanADay_IsNotFetchedAgain()
    {
        LiveAnswers(Ok, Newer);
        await _sut.GetSponsorsAsync();

        _clock.Advance(TimeSpan.FromHours(23));
        var sponsors = await _sut.GetSponsorsAsync();
        await _sut.Refresh;

        sponsors!.Updated.Should().Be("live");
        VerifyLiveFetches(1);
    }

    [Fact]
    public async Task GetSponsorsAsync_LiveListADayOld_ReturnsItAtOnceAndTheNextCallGetsTheNewList()
    {
        LiveAnswers(Ok, Newer);
        await _sut.GetSponsorsAsync();

        _clock.Advance(TimeSpan.FromHours(24));
        var stale = await _sut.GetSponsorsAsync();
        await _sut.Refresh;
        var fresh = await _sut.GetSponsorsAsync();

        stale!.Updated.Should().Be("live");
        fresh!.Updated.Should().Be("newer");
        VerifyLiveFetches(2);
    }

    [Fact]
    public async Task GetSponsorsAsync_FallbackListYoungerThanAnHour_IsNotFetchedAgain()
    {
        LiveAnswers(() => new HttpResponseMessage(HttpStatusCode.NotFound), Newer);
        OnDisk(BundledPath, """{"updated":"bundled"}""");
        await _sut.GetSponsorsAsync();

        _clock.Advance(TimeSpan.FromMinutes(59));
        await _sut.GetSponsorsAsync();
        await _sut.Refresh;

        VerifyLiveFetches(1);
    }

    [Fact]
    public async Task GetSponsorsAsync_FallbackListAnHourOld_IsReplacedByTheLiveList()
    {
        LiveAnswers(() => new HttpResponseMessage(HttpStatusCode.NotFound), Newer);
        OnDisk(BundledPath, """{"updated":"bundled"}""");
        await _sut.GetSponsorsAsync();

        _clock.Advance(TimeSpan.FromHours(1));
        var stale = await _sut.GetSponsorsAsync();
        await _sut.Refresh;
        var fresh = await _sut.GetSponsorsAsync();

        stale!.Updated.Should().Be("bundled");
        fresh!.Updated.Should().Be("newer");
    }

    [Fact]
    public async Task GetSponsorsAsync_RefetchFails_KeepsTheListAndTriesAgainAnHourLater()
    {
        LiveAnswers(Ok, () => new HttpResponseMessage(HttpStatusCode.NotFound), Newer);
        await _sut.GetSponsorsAsync();

        _clock.Advance(TimeSpan.FromHours(24));
        await _sut.GetSponsorsAsync();
        await _sut.Refresh;
        _clock.Advance(TimeSpan.FromMinutes(59));
        var kept = await _sut.GetSponsorsAsync();
        await _sut.Refresh;
        VerifyLiveFetches(2);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await _sut.GetSponsorsAsync();
        await _sut.Refresh;
        var fresh = await _sut.GetSponsorsAsync();

        kept!.Updated.Should().Be("live");
        fresh!.Updated.Should().Be("newer");
        VerifyLiveFetches(3);
    }

    [Fact]
    public async Task CacheLogosAsync_AfterALiveFetch_SavesEachLogo()
    {
        LiveServes(ListWithLogo);

        await _sut.GetSponsorsAsync();
        await _sut.CacheLogosAsync();

        _files.Verify(f => f.WriteAllBytesAsync(CachedLogoPath, It.Is<byte[]>(bytes => bytes.SequenceEqual(LogoBytes)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CacheLogosAsync_ListUnchangedAndLogoOnDisk_DownloadsNothing()
    {
        LiveServes(ListWithLogo);
        OnDisk(LastGoodPath, ListWithLogo);
        _files.Setup(f => f.FileExists(CachedLogoPath)).Returns(true);

        await _sut.GetSponsorsAsync();
        await _sut.CacheLogosAsync();

        _files.Verify(f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CacheLogosAsync_ListChanged_RefreshesALogoAlreadyOnDisk()
    {
        LiveServes(ListWithLogo);
        OnDisk(LastGoodPath, """{"sponsors":[]}""");
        _files.Setup(f => f.FileExists(CachedLogoPath)).Returns(true);

        await _sut.GetSponsorsAsync();
        await _sut.CacheLogosAsync();

        _files.Verify(f => f.WriteAllBytesAsync(CachedLogoPath, It.Is<byte[]>(bytes => bytes.SequenceEqual(LogoBytes)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CacheLogosAsync_ListCameFromAFallback_DoesNothing()
    {
        LiveAnswers(() => new HttpResponseMessage(HttpStatusCode.NotFound));
        OnDisk(BundledPath, ListWithLogo);

        await _sut.GetSponsorsAsync();
        await _sut.CacheLogosAsync();

        _files.Verify(f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetLogoSources_CachedThenBundledThenLive()
    {
        _files.Setup(f => f.FileExists(CachedLogoPath)).Returns(true);
        _files.Setup(f => f.FileExists(Path.Combine(Path.GetDirectoryName(BundledPath)!, "logos", "acme.png"))).Returns(true);

        var sources = _sut.GetLogoSources(new() { Name = "Acme", Logo = "logos/acme.png" });

        sources.Should().Equal(
            new Uri(CachedLogoPath).AbsoluteUri,
            "ms-appx:///Assets/Sponsors/logos/acme.png",
            LiveBase + "logos/acme.png");
    }

    [Fact]
    public void GetLogoSources_NothingOnDisk_OnlyTheLiveFile()
    {
        _sut.GetLogoSources(new() { Name = "Acme", Logo = "logos/acme.png" })
            .Should().Equal(LiveBase + "logos/acme.png");
    }

    private void LiveServes(string json)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => request.RequestUri!.AbsoluteUri.EndsWith(".png")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(LogoBytes) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new StringContent(LiveJson) };

    private static HttpResponseMessage Newer() => new(HttpStatusCode.OK) { Content = new StringContent("""{"updated":"newer"}""") };

    private static HttpResponseMessage Unavailable() => new(HttpStatusCode.ServiceUnavailable);

    private void VerifyLiveFetches(int count) =>
        _handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(count),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

    private void LiveAnswers(params Func<HttpResponseMessage>[] answers)
    {
        var queue = new Queue<Func<HttpResponseMessage>>(answers);
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => queue.Dequeue()());
    }

    private void OnDisk(string path, string json)
    {
        _files.Setup(f => f.FileExists(path)).Returns(true);
        _files.Setup(f => f.ReadAllTextAsync(path, It.IsAny<CancellationToken>())).ReturnsAsync(json);
    }
}
