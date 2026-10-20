using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class WimImageServiceTests
{
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IDismProcessRunner> _mockDismRunner = new();
    private readonly Mock<IDismImageInfoReader> _mockImageInfo = new();
    private readonly WimImageService _service;

    public WimImageServiceTests()
    {
        _mockFileSystem
            .Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockImageInfo
            .Setup(d => d.GetImageInfo(It.IsAny<string>()))
            .Returns([new DismImageEntry(1, "Windows 11 Pro")]);

        _service = new WimImageService(
            _mockFileSystem.Object,
            _mockLogService.Object,
            _mockLocalization.Object,
            _mockDismRunner.Object,
            _mockImageInfo.Object)
        {
            RetryDelay = TimeSpan.Zero,
        };
    }

    [Fact]
    public async Task DetectImageFormatAsync_SourcesDirectoryNotFound_ReturnsNull()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        var result = await _service.DetectImageFormatAsync(@"C:\work");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectImageFormatAsync_WimFileExists_ReturnsWimFormat()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("install.wim")))).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(5_000_000_000L);
        _mockImageInfo.Setup(d => d.GetImageInfo(@"C:\work\sources\install.wim"))
            .Returns([new DismImageEntry(1, "Windows 11 Pro", 26300), new DismImageEntry(2, "Windows 11 Home", 26300)]);

        var result = await _service.DetectImageFormatAsync(@"C:\work");

        result.Should().NotBeNull();
        result!.Format.Should().Be(ImageFormat.Wim);
        result.Editions.Should().HaveCount(2);
        result.Editions.Select(e => e.Name).Should().Equal("Windows 11 Pro", "Windows 11 Home");
        result.Build.Should().Be(26300);
    }

    [Fact]
    public async Task DetectImageFormatAsync_OnlyEsdFileExists_ReturnsEsdFormat()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("install.wim")))).Returns(false);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("install.esd")))).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(3_000_000_000L);

        var result = await _service.DetectImageFormatAsync(@"C:\work");

        result.Should().NotBeNull();
        result!.Format.Should().Be(ImageFormat.Esd);
    }

    [Fact]
    public async Task DetectImageFormatAsync_NoImageFiles_ReturnsNull()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        var result = await _service.DetectImageFormatAsync(@"C:\work");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectAllImageFormatsAsync_SourcesDirectoryNotFound_ReturnsEmptyResult()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        var result = await _service.DetectAllImageFormatsAsync(@"C:\work");

        result.NeitherExists.Should().BeTrue();
    }

    [Fact]
    public async Task DetectAllImageFormatsAsync_BothFormatsExist_ReturnsBothAndLogsWarning()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(1_000_000L);

        var result = await _service.DetectAllImageFormatsAsync(@"C:\work");

        result.BothExist.Should().BeTrue();
        _mockLogService.Verify(
            l => l.LogWarning(It.Is<string>(s => s.Contains("Both install.wim and install.esd")), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteImageFileAsync_FileNotFound_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        var result = await _service.DeleteImageFileAsync(@"C:\work", ImageFormat.Wim);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteImageFileAsync_FileDeletedSuccessfully_ReturnsTrue()
    {
        var deleteCallCount = 0;
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(() =>
        {
            return deleteCallCount == 0;
        });
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(5_000_000_000L);
        _mockFileSystem.Setup(fs => fs.SetFileAttributes(It.IsAny<string>(), It.IsAny<System.IO.FileAttributes>()));
        _mockFileSystem.Setup(fs => fs.DeleteFile(It.IsAny<string>())).Callback(() => deleteCallCount++);

        var result = await _service.DeleteImageFileAsync(@"C:\work", ImageFormat.Wim);

        result.Should().BeTrue();
        _mockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DetectImageFormatAsync_ReadsEditionNamesFromTheApiNotStdout()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("install.wim")))).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(5_000_000_000L);
        _mockImageInfo.Setup(d => d.GetImageInfo(@"C:\work\sources\install.wim"))
            .Returns([new DismImageEntry(1, "Windows 11 Home"), new DismImageEntry(2, "Windows 11 Pro")]);

        var info = await _service.DetectImageFormatAsync(@"C:\work");

        info!.Editions.Should().HaveCount(2);
        info.Editions.Select(e => e.Name).Should().Equal("Windows 11 Home", "Windows 11 Pro");
    }

    private const string Wim = @"C:\work\sources\install.wim";
    private const string KeepWim = @"C:\work\sources\install.keep.wim";
    private const string OldWim = @"C:\work\sources\install.old.wim";
    private const string Home = "Windows 11 Home";
    private const string HomeSingleLanguage = "Windows 11 Home Single Language";
    private const string Pro = "Windows 11 Pro";

    // A disk of file names: DISM's export creates the destination, moves and deletes act on the set.
    private HashSet<string> SimulateDisk(params string[] files)
    {
        var disk = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns((string p) => disk.Contains(p));
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(5_000_000_000L);
        _mockFileSystem.Setup(fs => fs.GetFileName(It.IsAny<string>())).Returns((string p) => p.Split('\\').Last());
        _mockFileSystem.Setup(fs => fs.DeleteFile(It.IsAny<string>())).Callback((string p) => disk.Remove(p));
        _mockFileSystem.Setup(fs => fs.MoveFile(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string from, string to) =>
            {
                if (!disk.Remove(from)) throw new IOException($"{from} not found");
                disk.Add(to);
            });
        _mockDismRunner.Setup(r => r.CheckDiskSpaceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>())).ReturnsAsync(true);
        _mockDismRunner
            .Setup(r => r.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string args, IProgress<TaskProgressDetail>? _, CancellationToken _) =>
                disk.Add(args.Split("/DestinationImageFile:\"")[1].Split('"')[0]))
            .ReturnsAsync((0, string.Empty));
        _mockImageInfo.Setup(d => d.GetImageInfo(It.IsAny<string>())).Returns(
        [
            new DismImageEntry(1, "Windows 11 Home", 26300, "Core"),
            new DismImageEntry(2, "Windows 11 Home Single Language", 26300, "CoreSingleLanguage"),
            new DismImageEntry(3, "Windows 11 Pro", 26300, "Professional"),
        ]);
        return disk;
    }

    private void VerifyExported(params int[] indexes)
    {
        foreach (var index in indexes)
            _mockDismRunner.Verify(r => r.RunProcessWithProgressAsync("dism.exe",
                It.Is<string>(a => a.Contains($"/SourceIndex:{index} ") && a.Contains(KeepWim)),
                It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDismRunner.Verify(r => r.RunProcessWithProgressAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Exactly(indexes.Length));
    }

    [Fact]
    public async Task DetectImageFormatAsync_CarriesEachEditionsIndexAndEditionId()
    {
        SimulateDisk(Wim);

        var info = await _service.DetectImageFormatAsync(@"C:\work");

        info!.Editions.Should().Equal(
            new ImageEdition(1, "Windows 11 Home", "Core"),
            new ImageEdition(2, "Windows 11 Home Single Language", "CoreSingleLanguage"),
            new ImageEdition(3, "Windows 11 Pro", "Professional"));
    }

    [Fact]
    public async Task KeepEditionsAsync_ExportsOnlyTheKeptEditionsAndSwapsTheFileIn()
    {
        var disk = SimulateDisk(Wim);

        var result = await _service.KeepEditionsAsync(@"C:\work", [Home, Pro]);

        result.Should().Be(new EditionRemovalResult(true));
        VerifyExported(1, 3);
        _mockDismRunner.Verify(r => r.RunProcessWithProgressAsync("dism.exe", It.Is<string>(a => a.Contains("/Compress:max")),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        disk.Should().BeEquivalentTo([Wim]);
        _mockFileSystem.Verify(fs => fs.MoveFile(Wim, OldWim), Times.Once);
        _mockFileSystem.Verify(fs => fs.MoveFile(KeepWim, Wim), Times.Once);
    }

    [Fact]
    public async Task KeepEditionsAsync_KeepsAnEsdAnEsd()
    {
        const string esd = @"C:\work\sources\install.esd";
        var disk = SimulateDisk(esd);

        var result = await _service.KeepEditionsAsync(@"C:\work", [HomeSingleLanguage]);

        result.Should().Be(new EditionRemovalResult(true));
        _mockDismRunner.Verify(r => r.RunProcessWithProgressAsync("dism.exe",
            It.Is<string>(a => a.Contains("/SourceIndex:2 ") && a.Contains("install.keep.esd") && a.Contains("/Compress:recovery")),
            It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
        disk.Should().BeEquivalentTo([esd]);
    }

    [Fact]
    public async Task KeepEditionsAsync_ClearsAFileLeftByAnInterruptedRunBeforeExporting()
    {
        var disk = SimulateDisk(Wim, KeepWim);

        await _service.KeepEditionsAsync(@"C:\work", [Pro]);

        _mockFileSystem.Verify(fs => fs.DeleteFile(KeepWim), Times.Once);
        disk.Should().BeEquivalentTo([Wim]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(false, "Windows 11 Enterprise")]
    [InlineData(true, Home, HomeSingleLanguage, Pro)]
    public async Task KeepEditionsAsync_LeavesTheImageAloneWhenThereIsNothingToRemove(bool expected, params string[] names)
    {
        var disk = SimulateDisk(Wim);

        var result = await _service.KeepEditionsAsync(@"C:\work", names);

        result.Should().Be(new EditionRemovalResult(expected));
        VerifyExported();
        disk.Should().BeEquivalentTo([Wim]);
    }

    [Fact]
    public async Task KeepEditionsAsync_DismFailure_DeletesThePartialFileAndKeepsTheOriginal()
    {
        var disk = SimulateDisk(Wim);
        _mockDismRunner
            .Setup(r => r.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => disk.Add(KeepWim))
            .ReturnsAsync((5, string.Empty));

        var result = await _service.KeepEditionsAsync(@"C:\work", [Pro]);

        result.Should().Be(EditionRemovalResult.Failed);
        disk.Should().BeEquivalentTo([Wim]);
        _mockFileSystem.Verify(fs => fs.MoveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task KeepEditionsAsync_PutsTheOriginalBackWhenTheNewFileCannotTakeItsName()
    {
        var disk = SimulateDisk(Wim);
        _mockFileSystem.Setup(fs => fs.MoveFile(KeepWim, Wim)).Throws(new IOException("in use"));

        var result = await _service.KeepEditionsAsync(@"C:\work", [Pro]);

        result.Should().Be(EditionRemovalResult.Failed);
        disk.Should().Contain(Wim);
        disk.Should().NotContain([OldWim, KeepWim]);
    }

    [Fact]
    public async Task KeepEditionsAsync_RunAgainWithTheSameNames_KeepsTheSameEditions()
    {
        var disk = SimulateDisk(Wim);
        await _service.KeepEditionsAsync(@"C:\work", [Home, Pro]);
        _mockImageInfo.Setup(d => d.GetImageInfo(It.IsAny<string>())).Returns(
        [
            new DismImageEntry(1, Home, 26300, "Core"),
            new DismImageEntry(2, Pro, 26300, "Professional"),
        ]);
        _mockDismRunner.Invocations.Clear();

        var result = await _service.KeepEditionsAsync(@"C:\work", [Home, Pro]);

        result.Should().Be(new EditionRemovalResult(true));
        VerifyExported();
        disk.Should().BeEquivalentTo([Wim]);
    }

    [Fact]
    public async Task KeepEditionsAsync_WhenTheOldImageCannotBeDeleted_ReportsWhereItIs()
    {
        var disk = SimulateDisk(Wim);
        _mockFileSystem.Setup(fs => fs.DeleteFile(OldWim)).Throws(new IOException("in use"));

        var result = await _service.KeepEditionsAsync(@"C:\work", [Pro]);

        result.Should().Be(new EditionRemovalResult(true, OldWim));
        disk.Should().BeEquivalentTo([Wim, OldWim]);
    }

    [Fact]
    public async Task KeepEditionsAsync_CancelledDuringTheExport_LeavesTheOriginalImage()
    {
        var disk = SimulateDisk(Wim);
        using var cts = new CancellationTokenSource();
        _mockDismRunner
            .Setup(r => r.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), cts.Token))
            .Callback(() => disk.Add(KeepWim))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _service.KeepEditionsAsync(@"C:\work", [Pro], null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        disk.Should().BeEquivalentTo([Wim]);
        _mockFileSystem.Verify(fs => fs.MoveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task KeepEditionsAsync_CancelledOnceTheExportIsDone_StillLeavesTheOriginalImage()
    {
        var disk = SimulateDisk(Wim);
        using var cts = new CancellationTokenSource();
        _mockDismRunner
            .Setup(r => r.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                disk.Add(KeepWim);
                cts.Cancel();
            })
            .ReturnsAsync((0, string.Empty));

        var act = () => _service.KeepEditionsAsync(@"C:\work", [Pro], null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        disk.Should().BeEquivalentTo([Wim]);
        _mockFileSystem.Verify(fs => fs.MoveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // Past the swap the editions are gone, so a cancel must not turn a finished removal into a cancelled one.
    [Fact]
    public async Task KeepEditionsAsync_CancelledAfterTheSwap_ReportsTheRemovalAndTheOldImage()
    {
        var disk = SimulateDisk(Wim);
        using var cts = new CancellationTokenSource();
        _mockFileSystem.Setup(fs => fs.MoveFile(KeepWim, Wim)).Callback(() =>
        {
            disk.Remove(KeepWim);
            disk.Add(Wim);
            cts.Cancel();
        });
        _mockFileSystem.Setup(fs => fs.DeleteFile(OldWim)).Throws(new IOException("in use"));

        var result = await _service.KeepEditionsAsync(@"C:\work", [Pro], null, cts.Token);

        result.Should().Be(new EditionRemovalResult(true, OldWim));
        disk.Should().BeEquivalentTo([Wim, OldWim]);
    }

    [Fact]
    public async Task DetectImageFormatAsync_ReturnsNullWhenTheApiReportsNoImages()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("install.wim")))).Returns(true);
        _mockImageInfo.Setup(d => d.GetImageInfo(It.IsAny<string>())).Returns([]);

        var info = await _service.DetectImageFormatAsync(@"C:\work");

        info.Should().BeNull();
    }

    [Fact]
    public async Task DetectImageFormatAsync_ReturnsNullWhenTheApiThrows()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("install.wim")))).Returns(true);
        _mockImageInfo.Setup(d => d.GetImageInfo(It.IsAny<string>()))
            .Throws(new InvalidOperationException("dismapi refused"));

        var info = await _service.DetectImageFormatAsync(@"C:\work");

        info.Should().BeNull();
    }


    [Fact]
    public async Task ConvertImageAsync_ForwardsTheCallersTokenToTheDismRunner()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("install.wim")))).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(5_000_000_000L);
        _mockDismRunner.Setup(d => d.CheckDiskSpaceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockDismRunner.Setup(d => d.RunProcessWithProgressAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, string.Empty));
        using var cts = new CancellationTokenSource();

        await _service.ConvertImageAsync(@"C:\work", ImageFormat.Esd, null, cts.Token);

        // The runner's token registration is what kills the child DISM process on cancellation,
        // so forwarding it is the whole cancellation contract.
        _mockDismRunner.Verify(d => d.RunProcessWithProgressAsync(
            "dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), cts.Token), Times.AtLeastOnce);
    }
}
