using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class OscdimgToolManagerTests
{
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<HttpMessageHandler> _mockHttpHandler = new();
    private readonly HttpClient _httpClient;
    private readonly Mock<IWinGetPackageInstaller> _mockWinGetInstaller = new();
    private readonly Mock<IWinGetBootstrapper> _mockWinGetBootstrapper = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IDismProcessRunner> _mockDismRunner = new();
    private readonly OscdimgToolManager _service;

    public OscdimgToolManagerTests()
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

        _service = new OscdimgToolManager(
            _mockFileSystem.Object,
            _mockLogService.Object,
            _httpClient,
            _mockWinGetInstaller.Object,
            _mockWinGetBootstrapper.Object,
            _mockLocalization.Object,
            _mockDismRunner.Object);
    }

    #region GetOscdimgPath

    [Fact]
    public void GetOscdimgPath_FoundInAdkPath_ReturnsPath()
    {
        // Arrange - make the first ADK path return true
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(
                p => p.Contains("Windows Kits") && p.Contains("amd64"))))
            .Returns(true);

        // Act
        var result = _service.GetOscdimgPath();

        // Assert
        result.Should().Contain("Windows Kits");
        result.Should().Contain("oscdimg.exe");
    }

    [Fact]
    public void GetOscdimgPath_NotFoundAnywhere_ReturnsEmptyString()
    {
        // Arrange - no files exist
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = _service.GetOscdimgPath();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetOscdimgPath_FoundInWingetPackagesDir_ReturnsPath()
    {
        // Arrange - known search paths return false
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        // WinGet packages directory exists
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.Is<string>(p => p.Contains("WinGet\\Packages"))))
            .Returns(true);
        _mockFileSystem.Setup(fs => fs.GetDirectories(
            It.Is<string>(p => p.Contains("WinGet\\Packages")),
            It.Is<string>(p => p.Contains("Microsoft.OSCDIMG"))))
            .Returns(new[] { @"C:\Program Files\WinGet\Packages\Microsoft.OSCDIMG_1.0" });
        // The candidate inside the matched dir
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(
            p => p.Contains("Microsoft.OSCDIMG_1.0") && p.Contains("oscdimg.exe"))))
            .Returns(true);

        // Act
        var result = _service.GetOscdimgPath();

        // Assert
        result.Should().Contain("Microsoft.OSCDIMG_1.0");
        result.Should().Contain("oscdimg.exe");
    }

    #endregion

    #region IsOscdimgAvailableAsync

    [Fact]
    public async Task IsOscdimgAvailableAsync_PathFound_ReturnsTrue()
    {
        // Arrange
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("Windows Kits"))))
            .Returns(true);

        // Act
        var result = await _service.IsOscdimgAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOscdimgAvailableAsync_PathNotFound_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _service.IsOscdimgAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region EnsureOscdimgAvailableAsync

    [Fact]
    public async Task EnsureOscdimgAvailableAsync_AlreadyAvailable_ReturnsTrueWithoutInstalling()
    {
        // Arrange - oscdimg already available
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("Windows Kits"))))
            .Returns(true);

        // Act
        var result = await _service.EnsureOscdimgAvailableAsync();

        // Assert
        result.Should().BeTrue();
        // Should not have tried to install anything
        _mockWinGetInstaller.Verify(
            w => w.IsWinGetInstalledAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
