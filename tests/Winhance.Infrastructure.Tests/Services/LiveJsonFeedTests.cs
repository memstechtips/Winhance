using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class LiveJsonFeedTests
{
    private const string LocalAppData = @"C:\Users\Test\AppData\Local";

    private static readonly string LastGoodFolder = Path.Combine(LocalAppData, "Winhance", "Cache");
    private static readonly string LastGoodPath = Path.Combine(LastGoodFolder, "feed.json");

    private readonly Mock<HttpMessageHandler> _handler = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly List<int> _waits = [];
    private readonly LiveJsonFeed<string> _sut;

    public LiveJsonFeedTests()
    {
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => Path.Combine(parts));

        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser
            .Setup(u => u.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(LocalAppData);

        _sut = new LiveJsonFeed<string>(
            new HttpClient(_handler.Object),
            Mock.Of<ILogService>(),
            _files.Object,
            interactiveUser.Object,
            (ms, _) =>
            {
                _waits.Add(ms);
                return Task.CompletedTask;
            },
            "test list",
            "https://example.test/feed.json",
            "Test-Loader",
            "feed.json",
            json => json.StartsWith("valid") ? json : null);
    }

    [Fact]
    public async Task FetchAsync_TwoServerErrors_RetriesAfterShortWaitsAndReturnsTheList()
    {
        Answers(Unavailable, Unavailable, () => Ok("valid-1"));

        var fetch = await _sut.FetchAsync(CancellationToken.None);

        fetch!.Value.Should().Be("valid-1");
        _waits.Should().Equal(500, 1000);
        VerifyRequests(3);
    }

    [Fact]
    public async Task FetchAsync_AllAttemptsFail_ReturnsNullAfterThreeRequests()
    {
        Answers(Unavailable, Unavailable, Unavailable);

        (await _sut.FetchAsync(CancellationToken.None)).Should().BeNull();

        VerifyRequests(3);
    }

    [Fact]
    public async Task FetchAsync_ClientError_IsNotRetried()
    {
        Answers(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        (await _sut.FetchAsync(CancellationToken.None)).Should().BeNull();

        _waits.Should().BeEmpty();
        VerifyRequests(1);
    }

    [Fact]
    public async Task FetchAsync_InvalidBody_IsNotRetriedAndNotSaved()
    {
        Answers(() => Ok("garbage"));

        (await _sut.FetchAsync(CancellationToken.None)).Should().BeNull();

        _waits.Should().BeEmpty();
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FetchAsync_ConnectionFailure_IsRetried()
    {
        Answers(() => throw new HttpRequestException("reset"), () => Ok("valid-2"));

        (await _sut.FetchAsync(CancellationToken.None))!.Value.Should().Be("valid-2");

        _waits.Should().Equal(500);
    }

    [Fact]
    public async Task FetchAsync_Success_SavesTheLastGoodCopyAndReportsThePreviousOne()
    {
        OnDisk(LastGoodPath, "valid-old");
        Answers(() => Ok("valid-new"));

        var fetch = await _sut.FetchAsync(CancellationToken.None);

        fetch!.Json.Should().Be("valid-new");
        fetch.PreviousJson.Should().Be("valid-old");
        _files.Verify(f => f.CreateDirectory(LastGoodFolder), Times.Once);
        _files.Verify(f => f.WriteAllTextAsync(LastGoodPath, "valid-new", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_NoSavedCopy_ReportsNoPreviousJson()
    {
        Answers(() => Ok("valid-new"));

        (await _sut.FetchAsync(CancellationToken.None))!.PreviousJson.Should().BeNull();
    }

    [Fact]
    public async Task LoadLastGoodAsync_ValidSavedCopy_ReturnsIt()
    {
        OnDisk(LastGoodPath, "valid-saved");

        (await _sut.LoadLastGoodAsync()).Should().Be("valid-saved");
    }

    [Fact]
    public async Task LoadAsync_InvalidFile_ReturnsNull()
    {
        OnDisk("bundled.json", "garbage");

        (await _sut.LoadAsync("bundled.json", "the bundled snapshot")).Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsNull()
    {
        (await _sut.LoadAsync("missing.json", "the bundled snapshot")).Should().BeNull();
    }

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static HttpResponseMessage Unavailable() => new(HttpStatusCode.ServiceUnavailable);

    private void Answers(params Func<HttpResponseMessage>[] answers)
    {
        var queue = new Queue<Func<HttpResponseMessage>>(answers);
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => queue.Dequeue()());
    }

    private void VerifyRequests(int count) =>
        _handler.Protected().Verify(
            "SendAsync", Times.Exactly(count), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());

    private void OnDisk(string path, string json)
    {
        _files.Setup(f => f.FileExists(path)).Returns(true);
        _files.Setup(f => f.ReadAllTextAsync(path, It.IsAny<CancellationToken>())).ReturnsAsync(json);
    }
}
