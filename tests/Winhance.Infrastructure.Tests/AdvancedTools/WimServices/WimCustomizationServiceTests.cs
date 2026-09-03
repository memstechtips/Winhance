using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class WimCustomizationServiceTests
{
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<HttpMessageHandler> _mockHttpHandler = new();
    private readonly HttpClient _httpClient;
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IDriverCategorizer> _mockDriverCategorizer = new();
    private readonly Mock<IDismProcessRunner> _mockDismRunner = new();
    private readonly Mock<IDriverInstallStepWriter> _mockDriverInstallStep = new();
    private readonly WimCustomizationService _service;

    public WimCustomizationServiceTests()
    {
        _httpClient = new HttpClient(_mockHttpHandler.Object);

        _mockFileSystem
            .Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _service = new WimCustomizationService(
            _mockFileSystem.Object,
            _mockLogService.Object,
            _httpClient,
            _mockLocalization.Object,
            _mockDriverCategorizer.Object,
            _mockDismRunner.Object,
            _mockDriverInstallStep.Object);
    }

    // The DownloadUnattendedWinstallXmlAsync path-validation tests guard issue #506.
    [Fact]
    public async Task DownloadUnattendedWinstallXmlAsync_EmptyDestinationPath_ThrowsArgumentException()
    {
        var act = () => _service.DownloadUnattendedWinstallXmlAsync(string.Empty);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("destinationPath");
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlAsync_NullDestinationPath_ThrowsArgumentException()
    {
        var act = () => _service.DownloadUnattendedWinstallXmlAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("destinationPath");
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlAsync_FileNameOnly_ThrowsArgumentException()
    {
        // GetDirectoryName returns empty string for a bare filename
        _mockFileSystem.Setup(fs => fs.GetDirectoryName("autounattend.xml")).Returns(string.Empty);

        var act = () => _service.DownloadUnattendedWinstallXmlAsync("autounattend.xml");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("destinationPath");
    }

    [Fact]
    public async Task DownloadUnattendedWinstallXmlAsync_DirectoryNameReturnsNull_ThrowsArgumentException()
    {
        _mockFileSystem.Setup(fs => fs.GetDirectoryName("autounattend.xml")).Returns((string?)null);

        var act = () => _service.DownloadUnattendedWinstallXmlAsync("autounattend.xml");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("destinationPath");
    }

    [Fact]
    public async Task AddXmlToImageAsync_XmlFileNotFound_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        var result = await _service.AddXmlToImageAsync(@"C:\missing.xml", @"C:\work");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddXmlToImageAsync_WorkingDirectoryNotFound_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\missing_dir");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddXmlToImageAsync_ValidInputs_CopiesFileAndReturnsTrue()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<xml>content</xml>");
        _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        result.Should().BeTrue();
        _mockFileSystem.Verify(
            fs => fs.WriteAllTextAsync(
                It.Is<string>(p => p.Contains("autounattend.xml")),
                "<xml>content</xml>",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddXmlToImageAsync_WriteThrows_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error"));

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddDriversAsync_Extract_ExportsStraightIntoTheOemFolder()
    {
        _mockDismRunner
            .Setup(d => d.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, ""));
        _mockDriverCategorizer
            .Setup(d => d.MoveStorageDrivers(@"C:\work\sources\$OEM$\$$\Drivers", @"C:\work\$WinpeDriver$"))
            .Returns(5);

        var result = await _service.AddDriversAsync(@"C:\work");

        result.Should().BeTrue();
        _mockFileSystem.Verify(fs => fs.CreateDirectory(@"C:\work\sources\$OEM$\$$\Drivers"), Times.Once);
        _mockDismRunner.Verify(d => d.RunProcessWithProgressAsync(
            "dism.exe",
            It.Is<string>(a => a.Contains("/Export-Driver") && a.Contains(@"C:\work\sources\$OEM$\$$\Drivers")),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDriverCategorizer.Verify(d => d.CategorizeAndCopyDrivers(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddDriversAsync_Extract_DismFailure_KeepsWhatLanded()
    {
        _mockDismRunner
            .Setup(d => d.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((87, "error"));
        _mockDriverCategorizer
            .Setup(d => d.MoveStorageDrivers(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(3);

        var result = await _service.AddDriversAsync(@"C:\work");

        result.Should().BeTrue();
        _mockLogService.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("87"))), Times.Once);
    }

    [Fact]
    public async Task AddDriversAsync_Extract_NothingExported_ReturnsFalse()
    {
        _mockDismRunner
            .Setup(d => d.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, ""));
        _mockDriverCategorizer
            .Setup(d => d.MoveStorageDrivers(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0);

        var result = await _service.AddDriversAsync(@"C:\work");

        result.Should().BeFalse();
        _mockDriverInstallStep.Verify(w => w.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDriversAsync_DriverSourcePathDoesNotExist_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.Is<string>(p => p == @"C:\drivers")))
            .Returns(false);

        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddDriversAsync_NoDriversCopied_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer.Setup(dc => dc.CategorizeAndCopyDrivers(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0);

        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddDriversAsync_DriversSuccessfullyCopied_ReturnsTrue()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer.Setup(dc => dc.CategorizeAndCopyDrivers(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(5);

        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddDriversAsync_StorageDriversGoToTheMediaRootNotTheSourcesFolder()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer
            .Setup(d => d.CategorizeAndCopyDrivers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(1);

        await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        _mockDriverCategorizer.Verify(d => d.CategorizeAndCopyDrivers(
            It.IsAny<string>(),
            @"C:\work\$WinpeDriver$",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AddDriversAsync_OnSuccess_EnsuresTheDriverInstallStep()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer
            .Setup(d => d.CategorizeAndCopyDrivers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(2);

        await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        _mockDriverInstallStep.Verify(w => w.EnsureAsync(@"C:\work", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddDriversAsync_NothingCopied_DoesNotTouchTheXml()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer
            .Setup(d => d.CategorizeAndCopyDrivers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0);

        await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        _mockDriverInstallStep.Verify(w => w.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDriversAsync_EnsureFailure_DoesNotFailTheOperation()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer
            .Setup(d => d.CategorizeAndCopyDrivers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(3);
        _mockDriverInstallStep
            .Setup(w => w.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("locked"));

        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddXmlToImageAsync_EnsureFailure_StillReturnsTrue()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<xml>content</xml>");
        _mockDriverInstallStep
            .Setup(w => w.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("locked"));

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddXmlToImageAsync_EnsuresTheDriverInstallStepAfterTheWrite()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<xml>content</xml>");

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        result.Should().BeTrue();
        _mockDriverInstallStep.Verify(w => w.EnsureAsync(@"C:\work", It.IsAny<CancellationToken>()), Times.Once);
    }
}
