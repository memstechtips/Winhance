using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class OscdimgToolManagerTests
{
    private static readonly string[] WinGetOscdimgPackageDir = [@"C:\Program Files\WinGet\Packages\Microsoft.OSCDIMG_1.0"];

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

    [Fact]
    public void GetOscdimgPath_FoundInAdkPath_ReturnsPath()
    {
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(
                p => p.Contains("Windows Kits") && p.Contains("amd64"))))
            .Returns(true);

        var result = _service.GetOscdimgPath();

        result.Should().Contain("Windows Kits");
        result.Should().Contain("oscdimg.exe");
    }

    [Fact]
    public void GetOscdimgPath_NotFoundAnywhere_ReturnsEmptyString()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        var result = _service.GetOscdimgPath();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetOscdimgPath_FoundInWingetPackagesDir_ReturnsPath()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.Is<string>(p => p.Contains("WinGet\\Packages"))))
            .Returns(true);
        _mockFileSystem.Setup(fs => fs.GetDirectories(
            It.Is<string>(p => p.Contains("WinGet\\Packages")),
            It.Is<string>(p => p.Contains("Microsoft.OSCDIMG"))))
            .Returns(WinGetOscdimgPackageDir);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(
            p => p.Contains("Microsoft.OSCDIMG_1.0") && p.Contains("oscdimg.exe"))))
            .Returns(true);

        var result = _service.GetOscdimgPath();

        result.Should().Contain("Microsoft.OSCDIMG_1.0");
        result.Should().Contain("oscdimg.exe");
    }

    [Fact]
    public async Task IsOscdimgAvailableAsync_PathFound_ReturnsTrue()
    {
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("Windows Kits"))))
            .Returns(true);

        var result = await _service.IsOscdimgAvailableAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOscdimgAvailableAsync_PathNotFound_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        var result = await _service.IsOscdimgAvailableAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureOscdimgAvailableAsync_AlreadyAvailable_ReturnsTrueWithoutInstalling()
    {
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(p => p.Contains("Windows Kits"))))
            .Returns(true);

        var result = await _service.EnsureOscdimgAvailableAsync();

        result.Should().BeTrue();
        _mockWinGetInstaller.Verify(
            w => w.IsWinGetInstalledAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
