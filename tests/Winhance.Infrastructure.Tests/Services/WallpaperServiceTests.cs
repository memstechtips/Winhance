using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
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

    [Fact]
    public async Task SetWallpaperAsync_WhenExceptionThrown_ReturnsFalseAndLogs()
    {
        // Use OTS path so the registry mock is called before P/Invoke,
        // then throw to exercise the catch block without invoking native APIs.
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(true);
        _mockRegistryService
            .Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Microsoft.Win32.RegistryValueKind>()))
            .Throws(new InvalidOperationException("simulated registry failure"));

        var result = await _service.SetWallpaperAsync(@"C:\nonexistent\path\wallpaper.jpg");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetWallpaperAsync_OtsElevation_WritesToRegistryAndSendsBroadcast()
    {
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(true);
        _mockSystemParametersService
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(1);
        var wallpaperPath = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";

        await _service.SetWallpaperAsync(wallpaperPath);

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
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockSystemParametersService
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(1);

        await _service.SetWallpaperAsync(@"C:\some\wallpaper.jpg");

        _mockRegistryService.Verify(
            r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Microsoft.Win32.RegistryValueKind>()),
            Times.Never);
    }

    [Fact]
    public async Task SetWallpaperAsync_Success_ReturnsTrueAndLogs()
    {
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockSystemParametersService
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(1);

        var result = await _service.SetWallpaperAsync(@"C:\some\wallpaper.jpg");

        result.Should().BeTrue();
        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(msg => msg.Contains("Wallpaper set to"))),
            Times.Once);
    }

    [Fact]
    public async Task SetWallpaperAsync_Failure_ReturnsFalseAndLogsError()
    {
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockSystemParametersService
            .Setup(s => s.SystemParametersInfo(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()))
            .Returns(0);

        var result = await _service.SetWallpaperAsync(@"C:\some\wallpaper.jpg");

        result.Should().BeFalse();
        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(msg => msg.Contains("Failed to set wallpaper"))),
            Times.Once);
    }
}
