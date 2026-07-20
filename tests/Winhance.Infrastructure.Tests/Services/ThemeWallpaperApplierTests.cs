// File: tests/Winhance.Infrastructure.Tests/Services/ThemeWallpaperApplierTests.cs
using System.Collections.Generic;
using Microsoft.Win32;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ThemeWallpaperApplierTests
{
    private readonly Mock<IWallpaperService> _wallpaper = new();
    private readonly Mock<IWindowsVersionService> _version = new();
    private readonly Mock<IStateWriter> _stateWriter = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IFileSystemService> _fs = new();
    private readonly ThemeWallpaperApplier _sut;

    public ThemeWallpaperApplierTests()
    {
        _stateWriter
            .Setup(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(true);
        _sut = new ThemeWallpaperApplier(
            _wallpaper.Object, _version.Object, _stateWriter.Object, _log.Object, _fs.Object);
    }

    [Fact]
    public async Task TryApply_NonThemeSettingId_ReturnsFalse()
    {

        var result = await _sut.TryApplySpecialSettingAsync("not-theme", 0);

        result.Should().BeFalse();
        _stateWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryApply_NonIntValue_ReturnsFalse()
    {

        var result = await _sut.TryApplySpecialSettingAsync(SettingIds.ThemeModeWindows, "dark");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryApply_DarkMode_WritesZeroToBothThemeKeys()
    {
        // The handler applies the catalog theme-mode-windows "Dark Mode" state: both
        // AppsUseLightTheme + SystemUsesLightTheme are written 0 via the state writer.

        await _sut.TryApplySpecialSettingAsync(SettingIds.ThemeModeWindows, 1);  // 1 = Dark

        _stateWriter.Verify(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(),
            It.Is<object>(v => v.Equals(0))), Times.Exactly(2));
    }

    [Fact]
    public async Task TryApply_LightMode_WritesOneToBothThemeKeys()
    {

        await _sut.TryApplySpecialSettingAsync(SettingIds.ThemeModeWindows, 0);  // 0 = Light

        _stateWriter.Verify(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(),
            It.Is<object>(v => v.Equals(1))), Times.Exactly(2));
    }

    [Fact]
    public async Task TryApply_WithAdditionalContext_AppliesWallpaper()
    {
        _version.Setup(v => v.IsWindows11()).Returns(true);
        _fs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        await _sut.TryApplySpecialSettingAsync(SettingIds.ThemeModeWindows, 1, additionalContext: true);

        _wallpaper.Verify(w => w.SetWallpaperAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task TryApply_WithoutAdditionalContext_DoesNotApplyWallpaper()
    {

        await _sut.TryApplySpecialSettingAsync(SettingIds.ThemeModeWindows, 1, additionalContext: false);

        _wallpaper.Verify(w => w.SetWallpaperAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryApply_WallpaperPathMissing_DoesNotCallSetWallpaper()
    {
        _version.Setup(v => v.IsWindows11()).Returns(true);
        _fs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        await _sut.TryApplySpecialSettingAsync(SettingIds.ThemeModeWindows, 1, additionalContext: true);

        _wallpaper.Verify(w => w.SetWallpaperAsync(It.IsAny<string>()), Times.Never);
    }
}
