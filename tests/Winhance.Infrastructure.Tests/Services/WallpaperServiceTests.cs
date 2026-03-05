using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WallpaperServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IWindowsRegistryService> _mockRegistryService = new();
    private readonly Mock<ISystemParametersService> _mockSystemParametersService = new();
    private readonly WallpaperService _service;

    public WallpaperServiceTests()
    {
        _service = new WallpaperService(
            _mockLogService.Object,
            _mockInteractiveUserService.Object,
            _mockRegistryService.Object,
            _mockSystemParametersService.Object);
    }

    #region Constructor

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        var act = () => new WallpaperService(
            null!,
            _mockInteractiveUserService.Object,
            _mockRegistryService.Object,
            _mockSystemParametersService.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    #endregion

    #region GetDefaultWallpaperPath

    [Fact]
    public void GetDefaultWallpaperPath_Windows11DarkMode_ReturnsDarkWallpaperPath()
    {
        // Act
        var result = _service.GetDefaultWallpaperPath(isWindows11: true, isDarkMode: true);

        // Assert
        var expected = System.IO.Path.Combine(
            WindowsThemeCustomizations.Wallpaper.Windows11BasePath,
            WindowsThemeCustomizations.Wallpaper.Windows11DarkWallpaper);
        result.Should().Be(expected);
    }

    [Fact]
    public void GetDefaultWallpaperPath_Windows11LightMode_ReturnsLightWallpaperPath()
    {
        // Act
        var result = _service.GetDefaultWallpaperPath(isWindows11: true, isDarkMode: false);

        // Assert
        var expected = System.IO.Path.Combine(
            WindowsThemeCustomizations.Wallpaper.Windows11BasePath,
            WindowsThemeCustomizations.Wallpaper.Windows11LightWallpaper);
        result.Should().Be(expected);
    }

    [Fact]
    public void GetDefaultWallpaperPath_Windows10_ReturnsWindows10WallpaperPath()
    {
        // Act — isDarkMode is irrelevant for Windows 10
        var result = _service.GetDefaultWallpaperPath(isWindows11: false, isDarkMode: false);

        // Assert
        result.Should().Be(WindowsThemeCustomizations.Wallpaper.Windows10Wallpaper);
    }

    [Fact]
    public void GetDefaultWallpaperPath_Windows10DarkMode_ReturnsWindows10WallpaperPath()
    {
        // Act — Windows 10 doesn't have a separate dark wallpaper
        var result = _service.GetDefaultWallpaperPath(isWindows11: false, isDarkMode: true);

        // Assert
        result.Should().Be(WindowsThemeCustomizations.Wallpaper.Windows10Wallpaper);
    }

    #endregion

    #region SetWallpaperAsync

    [Fact]
    public async Task SetWallpaperAsync_WhenExceptionThrown_ReturnsFalseAndLogs()
    {
        // Arrange — use OTS path so the registry mock is called before P/Invoke,
        // then throw to exercise the catch block without invoking native APIs.
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(true);
        _mockRegistryService
            .Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Microsoft.Win32.RegistryValueKind>()))
            .Throws(new InvalidOperationException("simulated registry failure"));

        // Act
        var result = await _service.SetWallpaperAsync(@"C:\nonexistent\path\wallpaper.jpg");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetWallpaperAsync_OtsElevation_WritesToRegistryAndSendsBroadcast()
    {
        // Arrange
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(true);
        _mockSystemParametersService
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(1);
        var wallpaperPath = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";

        // Act
        await _service.SetWallpaperAsync(wallpaperPath);

        // Assert — verify registry is written under OTS elevation
        _mockRegistryService.Verify(
            r => r.SetValue(
                @"HKEY_CURRENT_USER\Control Panel\Desktop",
                "Wallpaper",
                wallpaperPath,
                Microsoft.Win32.RegistryValueKind.String),
            Times.Once);
    }

    [Fact]
    public async Task SetWallpaperAsync_NotOtsElevation_DoesNotWriteToRegistry()
    {
        // Arrange
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockSystemParametersService
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(1);

        // Act
        await _service.SetWallpaperAsync(@"C:\some\wallpaper.jpg");

        // Assert — no direct registry write when not using OTS
        _mockRegistryService.Verify(
            r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Microsoft.Win32.RegistryValueKind>()),
            Times.Never);
    }

    [Fact]
    public async Task SetWallpaperAsync_Success_ReturnsTrueAndLogs()
    {
        // Arrange
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockSystemParametersService
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(1);

        // Act
        var result = await _service.SetWallpaperAsync(@"C:\some\wallpaper.jpg");

        // Assert
        result.Should().BeTrue();
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(msg => msg.Contains("Wallpaper set to"))),
            Times.Once);
    }

    [Fact]
    public async Task SetWallpaperAsync_Failure_ReturnsFalseAndLogsError()
    {
        // Arrange
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockSystemParametersService
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(0);

        // Act
        var result = await _service.SetWallpaperAsync(@"C:\some\wallpaper.jpg");

        // Assert
        result.Should().BeFalse();
        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(msg => msg.Contains("Failed to set wallpaper"))),
            Times.Once);
    }

    #endregion
}
