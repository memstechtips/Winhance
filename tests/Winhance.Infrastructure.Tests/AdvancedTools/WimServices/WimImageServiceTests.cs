using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

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
            _mockImageInfo.Object);
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
            .Returns([new DismImageEntry(1, "Windows 11 Pro"), new DismImageEntry(2, "Windows 11 Home")]);

        var result = await _service.DetectImageFormatAsync(@"C:\work");

        result.Should().NotBeNull();
        result!.Format.Should().Be(ImageFormat.Wim);
        result.ImageCount.Should().Be(2);
        result.EditionNames.Should().Equal("Windows 11 Pro", "Windows 11 Home");
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

        info!.ImageCount.Should().Be(2);
        info.EditionNames.Should().Equal("Windows 11 Home", "Windows 11 Pro");
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
