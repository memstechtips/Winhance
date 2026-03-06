using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigLoadServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IWindowsVersionService> _mockWindowsVersionService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<IConfigMigrationService> _mockConfigMigrationService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IMainWindowProvider> _mockMainWindowProvider = new();

    private ConfigLoadService CreateService()
    {
        return new ConfigLoadService(
            _mockLogService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockWindowsVersionService.Object,
            _mockCompatibleSettingsRegistry.Object,
            _mockConfigMigrationService.Object,
            _mockInteractiveUserService.Object,
            _mockFileSystemService.Object,
            _mockMainWindowProvider.Object);
    }

    // -------------------------------------------------------
    // DetectIncompatibleSettings
    // -------------------------------------------------------

    [Fact]
    public void DetectIncompatibleSettings_WithEmptyConfig_ReturnsEmptyList()
    {
        var service = CreateService();
        var config = new UnifiedConfigurationFile();

        var result = service.DetectIncompatibleSettings(config);

        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectIncompatibleSettings_WithWindows10OnlySetting_OnWindows11_ReturnsIncompatible()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22621);

        var settingDef = new SettingDefinition
        {
            Id = "win10-setting",
            Name = "Win10 Setting",
            Description = "Only for Win10",
            IsWindows10Only = true
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(new[] { settingDef });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "win10-setting", Name = "Win10 Setting" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().ContainSingle()
            .Which.Should().Be("Win10 Setting (TestFeature)");
    }

    [Fact]
    public void DetectIncompatibleSettings_WithWindows11OnlySetting_OnWindows10_ReturnsIncompatible()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(false);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(19045);

        var settingDef = new SettingDefinition
        {
            Id = "win11-setting",
            Name = "Win11 Setting",
            Description = "Only for Win11",
            IsWindows11Only = true
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(new[] { settingDef });

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "win11-setting", Name = "Win11 Setting" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().ContainSingle()
            .Which.Should().Be("Win11 Setting (TestFeature)");
    }

    [Fact]
    public void DetectIncompatibleSettings_WithMinimumBuildNumber_BelowThreshold_ReturnsIncompatible()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22000);

        var settingDef = new SettingDefinition
        {
            Id = "new-build-setting",
            Name = "New Build Setting",
            Description = "Requires newer build",
            MinimumBuildNumber = 22621
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(new[] { settingDef });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "new-build-setting", Name = "New Build Setting" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().ContainSingle();
    }

    [Fact]
    public void DetectIncompatibleSettings_WithMaximumBuildNumber_AboveThreshold_ReturnsIncompatible()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(30000);

        var settingDef = new SettingDefinition
        {
            Id = "old-build-setting",
            Name = "Old Build Setting",
            Description = "For older builds only",
            MaximumBuildNumber = 22621
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(new[] { settingDef });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "old-build-setting", Name = "Old Build Setting" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().ContainSingle();
    }

    [Fact]
    public void DetectIncompatibleSettings_WithSupportedBuildRanges_OutOfRange_ReturnsIncompatible()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(25000);

        var settingDef = new SettingDefinition
        {
            Id = "range-setting",
            Name = "Range Setting",
            Description = "Has build ranges",
            SupportedBuildRanges = new List<(int MinBuild, int MaxBuild)>
            {
                (22000, 22621),
                (26000, 30000)
            }
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(new[] { settingDef });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "range-setting", Name = "Range Setting" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().ContainSingle();
    }

    [Fact]
    public void DetectIncompatibleSettings_WithSupportedBuildRanges_InRange_ReturnsEmpty()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22500);

        var settingDef = new SettingDefinition
        {
            Id = "range-setting",
            Name = "Range Setting",
            Description = "Has build ranges",
            SupportedBuildRanges = new List<(int MinBuild, int MaxBuild)>
            {
                (22000, 22621)
            }
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(new[] { settingDef });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "range-setting", Name = "Range Setting" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectIncompatibleSettings_WithCompatibleSetting_ReturnsEmpty()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22621);

        var settingDef = new SettingDefinition
        {
            Id = "compatible-setting",
            Name = "Compatible Setting",
            Description = "Works everywhere"
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(new[] { settingDef });

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "compatible-setting", Name = "Compatible Setting" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectIncompatibleSettings_WithNullFeatures_SkipsSection()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22621);

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection { Features = new Dictionary<string, ConfigSection>() },
            Customize = new FeatureGroupSection { Features = new Dictionary<string, ConfigSection>() }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().BeEmpty();
    }

    // -------------------------------------------------------
    // FilterConfigForCurrentSystem
    // -------------------------------------------------------

    [Fact]
    public void FilterConfigForCurrentSystem_RemovesIncompatibleSettings()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22621);

        var compatibleDef = new SettingDefinition
        {
            Id = "compatible",
            Name = "Compatible",
            Description = "Works"
        };

        var incompatibleDef = new SettingDefinition
        {
            Id = "incompatible",
            Name = "Incompatible",
            Description = "Win10 only",
            IsWindows10Only = true
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(new[] { compatibleDef, incompatibleDef });

        var config = new UnifiedConfigurationFile
        {
            Version = "2.0",
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        IsIncluded = true,
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "compatible", Name = "Compatible" },
                            new ConfigurationItem { Id = "incompatible", Name = "Incompatible" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.FilterConfigForCurrentSystem(config);

        result.Version.Should().Be("2.0");
        result.Optimize.Features["TestFeature"].Items.Should().ContainSingle()
            .Which.Id.Should().Be("compatible");
    }

    [Fact]
    public void FilterConfigForCurrentSystem_KeepsSettingsNotInRegistry()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22621);

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetBypassedSettings("TestFeature"))
            .Returns(Enumerable.Empty<SettingDefinition>());

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["TestFeature"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "unknown", Name = "Unknown Setting" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.FilterConfigForCurrentSystem(config);

        result.Optimize.Features["TestFeature"].Items.Should().ContainSingle()
            .Which.Id.Should().Be("unknown");
    }

    [Fact]
    public void FilterConfigForCurrentSystem_PreservesWindowsAppsAndExternalApps()
    {
        _mockWindowsVersionService.Setup(w => w.IsWindows11()).Returns(true);
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22621);

        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                IsIncluded = true,
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "app1", Name = "App 1" }
                }
            },
            ExternalApps = new ConfigSection
            {
                IsIncluded = true,
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "ext-app1", Name = "External App 1" }
                }
            }
        };

        var service = CreateService();
        var result = service.FilterConfigForCurrentSystem(config);

        result.WindowsApps.Items.Should().ContainSingle();
        result.ExternalApps.Items.Should().ContainSingle();
    }

    // -------------------------------------------------------
    // LoadUserBackupConfigurationAsync
    // -------------------------------------------------------

    [Fact]
    public async Task LoadUserBackupConfigurationAsync_WhenDirectoryDoesNotExist_ShowsMessageAndReturnsNull()
    {
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\Test\AppData\Local");

        _mockFileSystemService
            .Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Users\Test\AppData\Local\Winhance\Backup");

        _mockFileSystemService
            .Setup(fs => fs.DirectoryExists(It.IsAny<string>()))
            .Returns(false);

        var service = CreateService();
        var result = await service.LoadUserBackupConfigurationAsync();

        result.Should().BeNull();
        _mockDialogService.Verify(d => d.ShowMessage(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task LoadUserBackupConfigurationAsync_WhenNoBackupFiles_ShowsMessageAndReturnsNull()
    {
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\Test\AppData\Local");

        _mockFileSystemService
            .Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Users\Test\AppData\Local\Winhance\Backup");

        _mockFileSystemService
            .Setup(fs => fs.DirectoryExists(It.IsAny<string>()))
            .Returns(true);

        _mockFileSystemService
            .Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Array.Empty<string>());

        var service = CreateService();
        var result = await service.LoadUserBackupConfigurationAsync();

        result.Should().BeNull();
        _mockDialogService.Verify(d => d.ShowMessage(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // -------------------------------------------------------
    // LoadAndValidateConfigurationFromFileAsync
    // -------------------------------------------------------

    [Fact]
    public async Task LoadAndValidateConfigurationFromFileAsync_WhenNoMainWindow_ReturnsNull()
    {
        _mockMainWindowProvider
            .Setup(p => p.MainWindow)
            .Returns((Microsoft.UI.Xaml.Window?)null);

        var service = CreateService();
        var result = await service.LoadAndValidateConfigurationFromFileAsync();

        result.Should().BeNull();
        _mockDialogService.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
