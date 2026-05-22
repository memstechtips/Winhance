// File: tests/Winhance.Infrastructure.Tests/Services/ThemeWallpaperApplierTests.cs
using System.Collections.Generic;
using Microsoft.Win32;
using FluentAssertions;
using Moq;
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
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IFileSystemService> _fs = new();
    private readonly ThemeWallpaperApplier _sut;

    public ThemeWallpaperApplierTests()
    {
        _sut = new ThemeWallpaperApplier(
            _wallpaper.Object, _version.Object, _registry.Object, _log.Object, _fs.Object);
    }

    [Fact]
    public async Task TryApply_NonThemeSettingId_ReturnsFalse()
    {
        var setting = new SettingDefinition { Id = "not-theme", Name = "not-theme", Description = "not-theme" };

        var result = await _sut.TryApplySpecialSettingAsync(setting, 0);

        result.Should().BeFalse();
        _registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryApply_NonIntValue_ReturnsFalse()
    {
        var setting = new SettingDefinition { Id = SettingIds.ThemeModeWindows, Name = "Theme", Description = "Theme" };

        var result = await _sut.TryApplySpecialSettingAsync(setting, "dark");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryApply_DarkMode_WritesZeroToRegistry()
    {
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "Test",
            RecommendedValue = 0,
            DefaultValue = 1,
            ValueType = RegistryValueKind.DWord,
        };
        var setting = new SettingDefinition
        {
            Id = SettingIds.ThemeModeWindows,
            Name = "Theme",
            Description = "Theme",
            RegistrySettings = new List<RegistrySetting> { regSetting }
        };

        await _sut.TryApplySpecialSettingAsync(setting, 1);  // 1 = Dark

        _registry.Verify(r => r.ApplySetting(regSetting, true, 0), Times.Once);
    }

    [Fact]
    public async Task TryApply_LightMode_WritesOneToRegistry()
    {
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "Test",
            RecommendedValue = 0,
            DefaultValue = 1,
            ValueType = RegistryValueKind.DWord,
        };
        var setting = new SettingDefinition
        {
            Id = SettingIds.ThemeModeWindows,
            Name = "Theme",
            Description = "Theme",
            RegistrySettings = new List<RegistrySetting> { regSetting }
        };

        await _sut.TryApplySpecialSettingAsync(setting, 0);  // 0 = Light

        _registry.Verify(r => r.ApplySetting(regSetting, true, 1), Times.Once);
    }

    [Fact]
    public async Task TryApply_WithAdditionalContext_AppliesWallpaper()
    {
        _version.Setup(v => v.IsWindows11()).Returns(true);
        _fs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        var setting = new SettingDefinition
        {
            Id = SettingIds.ThemeModeWindows,
            Name = "Theme",
            Description = "Theme",
            RegistrySettings = new List<RegistrySetting>()
        };

        await _sut.TryApplySpecialSettingAsync(setting, 1, additionalContext: true);

        _wallpaper.Verify(w => w.SetWallpaperAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task TryApply_WithoutAdditionalContext_DoesNotApplyWallpaper()
    {
        var setting = new SettingDefinition
        {
            Id = SettingIds.ThemeModeWindows,
            Name = "Theme",
            Description = "Theme",
            RegistrySettings = new List<RegistrySetting>()
        };

        await _sut.TryApplySpecialSettingAsync(setting, 1, additionalContext: false);

        _wallpaper.Verify(w => w.SetWallpaperAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryApply_WallpaperPathMissing_DoesNotCallSetWallpaper()
    {
        _version.Setup(v => v.IsWindows11()).Returns(true);
        _fs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        var setting = new SettingDefinition
        {
            Id = SettingIds.ThemeModeWindows,
            Name = "Theme",
            Description = "Theme",
            RegistrySettings = new List<RegistrySetting>()
        };

        await _sut.TryApplySpecialSettingAsync(setting, 1, additionalContext: true);

        _wallpaper.Verify(w => w.SetWallpaperAsync(It.IsAny<string>()), Times.Never);
    }
}
