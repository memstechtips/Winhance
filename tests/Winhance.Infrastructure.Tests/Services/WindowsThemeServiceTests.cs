using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WindowsThemeServiceTests
{
    private readonly Mock<IWallpaperService> _mockWallpaperService = new();
    private readonly Mock<IWindowsVersionService> _mockVersionService = new();
    private readonly Mock<IWindowsUIManagementService> _mockUiManagementService = new();
    private readonly Mock<IWindowsRegistryService> _mockRegistryService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly WindowsThemeService _service;

    public WindowsThemeServiceTests()
    {
        _service = new WindowsThemeService(
            _mockWallpaperService.Object,
            _mockVersionService.Object,
            _mockUiManagementService.Object,
            _mockRegistryService.Object,
            _mockLogService.Object,
            _mockCompatibleSettingsRegistry.Object,
            _mockConfigImportState.Object,
            _mockFileSystemService.Object);
    }

    #region DomainName

    [Fact]
    public void DomainName_ReturnsWindowsTheme()
    {
        _service.DomainName.Should().Be("WindowsTheme");
    }

    #endregion

    #region GetSettingsAsync

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettingsFromRegistry()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            new SettingDefinition
            {
                Id = "theme-mode-windows",
                Name = "Choose your mode",
                Description = "Light vs Dark"
            },
            new SettingDefinition
            {
                Id = "theme-transparency",
                Name = "Transparency effects",
                Description = "Enable translucent effects"
            }
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("WindowsTheme"))
            .Returns(settings);

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeSameAs(settings);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("WindowsTheme"))
            .Throws(new InvalidOperationException("Theme load failure"));

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Error,
                It.Is<string>(s => s.Contains("Windows theme") && s.Contains("Theme load failure"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_CalledTwice_ReturnsSameReference()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            new SettingDefinition
            {
                Id = "theme-mode-windows",
                Name = "Theme Mode",
                Description = "Test"
            }
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("WindowsTheme"))
            .Returns(settings);

        // Act
        var result1 = await _service.GetSettingsAsync();
        var result2 = await _service.GetSettingsAsync();

        // Assert
        result1.Should().BeSameAs(result2);
    }

    #endregion

    #region TryApplySpecialSettingAsync

    [Fact]
    public async Task TryApplySpecialSettingAsync_NonThemeModeSetting_ReturnsFalse()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "some-other-setting",
            Name = "Other",
            Description = "Not a theme mode"
        };

        // Act
        var result = await _service.TryApplySpecialSettingAsync(setting, 0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_ThemeModeSetting_NonIntValue_ReturnsFalse()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode"
        };

        // Act
        var result = await _service.TryApplySpecialSettingAsync(setting, "not-an-int");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_ThemeModeWithInt_ReturnsTrue()
    {
        // Arrange
        var registrySettings = new List<RegistrySetting>
        {
            new RegistrySetting
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                ValueName = "AppsUseLightTheme",
                ValueType = Microsoft.Win32.RegistryValueKind.DWord
            }
        };

        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode",
            RegistrySettings = registrySettings
        };

        _mockUiManagementService
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);

        // Act
        var result = await _service.TryApplySpecialSettingAsync(setting, 1);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region ApplyThemeModeWindowsAsync

    [Fact]
    public async Task ApplyThemeModeWindowsAsync_NonIntValue_ThrowsArgumentException()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode"
        };

        // Act
        var act = () => _service.ApplyThemeModeWindowsAsync(setting, "invalid");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*integer selection index*");
    }

    [Fact]
    public async Task ApplyThemeModeWindowsAsync_DarkMode_AppliesRegistrySettings()
    {
        // Arrange
        var registrySettings = new List<RegistrySetting>
        {
            new RegistrySetting
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                ValueName = "AppsUseLightTheme",
                ValueType = Microsoft.Win32.RegistryValueKind.DWord
            },
            new RegistrySetting
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                ValueName = "SystemUsesLightTheme",
                ValueType = Microsoft.Win32.RegistryValueKind.DWord
            }
        };

        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode",
            RegistrySettings = registrySettings
        };

        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        _mockUiManagementService
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act — apply dark mode (index 1)
        await _service.ApplyThemeModeWindowsAsync(setting, 1);

        // Assert — registry settings should be applied with value 0 (dark mode)
        _mockRegistryService.Verify(
            r => r.ApplySetting(It.IsAny<RegistrySetting>(), true, 0),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyThemeModeWindowsAsync_LightMode_AppliesRegistrySettings()
    {
        // Arrange
        var registrySettings = new List<RegistrySetting>
        {
            new RegistrySetting
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                ValueName = "AppsUseLightTheme",
                ValueType = Microsoft.Win32.RegistryValueKind.DWord
            }
        };

        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode",
            RegistrySettings = registrySettings
        };

        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        _mockUiManagementService
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act — apply light mode (index 0)
        await _service.ApplyThemeModeWindowsAsync(setting, 0);

        // Assert — registry settings should be applied with value 1 (light mode)
        _mockRegistryService.Verify(
            r => r.ApplySetting(It.IsAny<RegistrySetting>(), true, 1),
            Times.Once);
    }

    [Fact]
    public async Task ApplyThemeModeWindowsAsync_WithWallpaper_AppliesWallpaper()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode",
            RegistrySettings = new List<RegistrySetting>()
        };

        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockWallpaperService.Setup(w => w.SetWallpaperAsync(It.IsAny<string>())).ReturnsAsync(true);
        _mockUiManagementService
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act — apply dark mode with wallpaper
        await _service.ApplyThemeModeWindowsAsync(setting, 1, applyWallpaper: true);

        // Assert
        _mockWallpaperService.Verify(
            w => w.SetWallpaperAsync(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyThemeModeWindowsAsync_WithoutWallpaper_DoesNotApplyWallpaper()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode",
            RegistrySettings = new List<RegistrySetting>()
        };

        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        _mockUiManagementService
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _service.ApplyThemeModeWindowsAsync(setting, 0, applyWallpaper: false);

        // Assert
        _mockWallpaperService.Verify(
            w => w.SetWallpaperAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyThemeModeWindowsAsync_ConfigImportActive_RefreshesWithoutKillingExplorer()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode",
            RegistrySettings = new List<RegistrySetting>()
        };

        _mockConfigImportState.Setup(c => c.IsActive).Returns(true);
        _mockUiManagementService
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _service.ApplyThemeModeWindowsAsync(setting, 0);

        // Assert — killExplorer should be false when config import is active
        _mockUiManagementService.Verify(
            u => u.RefreshWindowsGUI(false),
            Times.Once);
    }

    [Fact]
    public async Task ApplyThemeModeWindowsAsync_ConfigImportNotActive_RefreshesWithKillingExplorer()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode",
            RegistrySettings = new List<RegistrySetting>()
        };

        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        _mockUiManagementService
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _service.ApplyThemeModeWindowsAsync(setting, 1);

        // Assert — killExplorer should be true when config import is not active
        _mockUiManagementService.Verify(
            u => u.RefreshWindowsGUI(true),
            Times.Once);
    }

    [Fact]
    public async Task ApplyThemeModeWindowsAsync_NoRegistrySettings_SkipsRegistryApplication()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "theme-mode-windows",
            Name = "Theme Mode",
            Description = "Choose mode"
            // RegistrySettings defaults to empty
        };

        _mockConfigImportState.Setup(c => c.IsActive).Returns(false);
        _mockUiManagementService
            .Setup(u => u.RefreshWindowsGUI(It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _service.ApplyThemeModeWindowsAsync(setting, 0);

        // Assert — no registry calls when there are no registry settings
        _mockRegistryService.Verify(
            r => r.ApplySetting(It.IsAny<RegistrySetting>(), It.IsAny<bool>(), It.IsAny<object>()),
            Times.Never);
    }

    #endregion
}
