using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil;

public class WindowsUpdateListServiceTests
{
    private const string LocalAppData = @"C:\Users\Test\AppData\Local";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string LastGoodFolder = Path.Combine(LocalAppData, "Winhance", "Cache");
    private static readonly string LastGoodPath = Path.Combine(LastGoodFolder, "updates.json");
    private static readonly string BundledPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Updates", "updates.json");

    private readonly Mock<HttpMessageHandler> _handler = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly WindowsUpdateListService _sut;

    public WindowsUpdateListServiceTests()
    {
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => Path.Combine(parts));

        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser
            .Setup(u => u.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(LocalAppData);

        _sut = new WindowsUpdateListService(
            new HttpClient(_handler.Object),
            Mock.Of<ILogService>(),
            _files.Object,
            interactiveUser.Object,
            _clock,
            (_, _) => Task.CompletedTask);
    }

    [Fact]
    public async Task FindUpdateAsync_BuildAndArchMatch_ReturnsTheUpdate()
    {
        LiveAnswers(Ok(Json(Build(26100, "x64", Update("KB5127648", 9711)))));

        var update = await _sut.FindUpdateAsync(26100, 1742, "x64");

        update!.Kb.Should().Be("KB5127648");
    }

    [Fact]
    public async Task FindUpdateAsync_ArchitectureDiffers_ReturnsNull()
    {
        LiveAnswers(Ok(Json(Build(26100, "x64", Update("KB5127648", 9711)))));

        (await _sut.FindUpdateAsync(26100, 1742, "arm64")).Should().BeNull();
    }

    [Fact]
    public async Task FindUpdateAsync_BuildDiffers_ReturnsNull()
    {
        LiveAnswers(Ok(Json(Build(26100, "x64", Update("KB5127648", 9711)))));

        (await _sut.FindUpdateAsync(26200, 1, "x64")).Should().BeNull();
    }

    [Theory]
    [InlineData(9711)]
    [InlineData(9800)]
    public async Task FindUpdateAsync_MediaIsAlreadyAtOrPastTheTarget_ReturnsNull(int mediaUbr)
    {
        LiveAnswers(Ok(Json(Build(26100, "x64", Update("KB5127648", 9711)))));

        (await _sut.FindUpdateAsync(26100, mediaUbr, "x64")).Should().BeNull();
    }

    [Fact]
    public async Task FindUpdateAsync_SeveralNewerUpdates_PicksTheHighestUbr()
    {
        LiveAnswers(Ok(Json(Build(26100, "x64", Update("KB5127648", 9711), Update("KB5127700", 9900), Update("KB5127600", 9500)))));

        var update = await _sut.FindUpdateAsync(26100, 9600, "X64");

        update!.Kb.Should().Be("KB5127700");
    }

    [Fact]
    public async Task FindUpdateAsync_ListHoldsABadEntry_NeverOffersIt()
    {
        var bad = Update("KB5127648", 9711) with { Kb = "KB5127649" };
        LiveAnswers(Ok(Json(Build(26100, "x64", bad))));

        (await _sut.FindUpdateAsync(26100, 1, "x64")).Should().BeNull();
    }

    [Fact]
    public async Task FindUpdateAsync_LiveFetchSucceeds_SavesItAsTheLastGoodList()
    {
        string json = Json(Build(26100, "x64", Update("KB5127648", 9711)));
        LiveAnswers(Ok(json));

        await _sut.FindUpdateAsync(26100, 1, "x64");

        _files.Verify(f => f.WriteAllTextAsync(LastGoodPath, json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindUpdateAsync_LiveFails_FallsBackToTheLastGoodListWhenItIsNewer()
    {
        LiveAnswers(Unavailable, Unavailable, Unavailable);
        OnDisk(LastGoodPath, Json(Build(26100, "x64", Update("KB5127648", 9711)), new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero)));
        OnDisk(BundledPath, Json(Build(26100, "x64", Update("KB5127000", 9000)), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));

        var update = await _sut.FindUpdateAsync(26100, 1, "x64");

        update!.Kb.Should().Be("KB5127648");
    }

    [Fact]
    public async Task FindUpdateAsync_LiveFails_UsesTheBundledListWhenAnAppUpdateMadeItNewer()
    {
        LiveAnswers(Unavailable, Unavailable, Unavailable);
        OnDisk(LastGoodPath, Json(Build(26100, "x64", Update("KB5127000", 9000)), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        OnDisk(BundledPath, Json(Build(26100, "x64", Update("KB5127648", 9711)), new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero)));

        var update = await _sut.FindUpdateAsync(26100, 1, "x64");

        update!.Kb.Should().Be("KB5127648");
    }

    [Fact]
    public async Task FindUpdateAsync_LiveFailsAndNothingWasSaved_UsesTheBundledList()
    {
        LiveAnswers(Unavailable, Unavailable, Unavailable);
        OnDisk(BundledPath, Json(Build(26100, "x64", Update("KB5127000", 9000))));

        var update = await _sut.FindUpdateAsync(26100, 1, "x64");

        update!.Kb.Should().Be("KB5127000");
    }

    [Fact]
    public async Task FindUpdateAsync_LiveListHasSchema2_IsNotSavedAndTheBundledListIsUsed()
    {
        var live = new WindowsUpdateList(2, DateTimeOffset.UtcNow, [Build(26100, "x64", Update("KB5127648", 9711))]);
        LiveAnswers(Ok(JsonSerializer.Serialize(live, JsonOptions)));
        OnDisk(BundledPath, Json(Build(26100, "x64", Update("KB5127000", 9000))));

        var update = await _sut.FindUpdateAsync(26100, 1, "x64");

        update!.Kb.Should().Be("KB5127000");
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"schema\":1,\"builds\":\"x\"}")]
    [InlineData("null")]
    [InlineData("")]
    public async Task FindUpdateAsync_GarbageEverywhere_ReturnsNullWithoutThrowing(string garbage)
    {
        LiveAnswers(Ok(garbage));
        OnDisk(LastGoodPath, garbage);
        OnDisk(BundledPath, garbage);

        var update = await _sut.FindUpdateAsync(26100, 1, "x64");

        update.Should().BeNull();
    }

    [Fact]
    public async Task FindUpdateAsync_SecondCallWithinTheLifetime_DoesNotFetchAgain()
    {
        LiveAnswers(Ok(Json(Build(26100, "x64", Update("KB5127648", 9711)))));

        await _sut.FindUpdateAsync(26100, 1, "x64");
        await _sut.FindUpdateAsync(26100, 1, "x64");

        _handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    private static WindowsUpdate Update(string kb, int ubr)
    {
        string digits = kb[2..];
        string sha1 = new('b', 40);
        string name = $"windows11.0-kb{digits}-x64_{sha1}.msu";
        var target = new WindowsUpdateFile(
            "target",
            kb,
            name,
            $"https://catalog.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/4225b261-8248-43e5-a808-34f3faafe3f6/public/{name}",
            1000,
            sha1,
            new string('c', 64));
        return new WindowsUpdate(kb, "Cumulative Update", ubr, new DateOnly(2026, 10, 14), "8d5353b2-7db6-451a-a42c-2dc9c486372d", [target]);
    }

    private static WindowsUpdateBuild Build(int build, string arch, params WindowsUpdate[] updates) =>
        new(build, arch, "Windows 11", updates);

    private static string Json(WindowsUpdateBuild build, DateTimeOffset? generatedAt = null) =>
        JsonSerializer.Serialize(new WindowsUpdateList(1, generatedAt ?? DateTimeOffset.UtcNow, [build]), JsonOptions);

    private static Func<HttpResponseMessage> Ok(string json) => () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };

    private static HttpResponseMessage Unavailable() => new(HttpStatusCode.ServiceUnavailable);

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
