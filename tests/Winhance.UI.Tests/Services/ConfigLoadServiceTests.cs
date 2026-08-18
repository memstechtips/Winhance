using FluentAssertions;
using Moq;
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
    private readonly Mock<IConfigMigrationService> _mockConfigMigrationService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IMainWindowProvider> _mockMainWindowProvider = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();

    private ConfigLoadService CreateService()
    {
        return new ConfigLoadService(
            _mockLogService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockWindowsVersionService.Object,
            _mockConfigMigrationService.Object,
            _mockInteractiveUserService.Object,
            _mockFileSystemService.Object,
            _mockMainWindowProvider.Object,
            _mockConfigImportState.Object);
    }

    // Gating is the catalog Availability model over the LIVE SettingCatalog (static, not
    // mockable), so fixtures use REAL catalog ids + mocked build numbers - machine-independent, since
    // Availability is authored data. Real ids used: privacy-timeline-suggestions (Windows10-gated,
    // "Timeline Suggestions"), start-recommended-section (Windows11-gated, "Recommended section"),
    // explorer-context-menu-compress-to (builds 26100+, 24H2), gaming-game-mode (ungated).

    [Fact]
    public void DetectIncompatibleSettings_WithEmptyConfig_ReturnsEmptyList()
    {
        var service = CreateService();
        var config = new UnifiedConfigurationFile();

        var result = service.DetectIncompatibleSettings(config);

        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectIncompatibleSettings_CatalogWindows10OnlySetting_OnWindows11_ReturnsIncompatible()
    {
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22631);

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "privacy-timeline-suggestions", Name = "Timeline Suggestions" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        // The name comes from the catalog Display.Name.
        result.Should().ContainSingle()
            .Which.Should().Be("Timeline Suggestions (Privacy)");
    }

    [Fact]
    public void DetectIncompatibleSettings_CatalogWindows11OnlySetting_OnWindows10_ReturnsIncompatible()
    {
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(19045);

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["StartMenu"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "start-recommended-section", Name = "Recommended section" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().ContainSingle()
            .Which.Should().Be("Recommended section (StartMenu)");
    }

    [Fact]
    public void DetectIncompatibleSettings_CatalogBuildRangeGatedSetting_BelowRange_ReturnsIncompatible()
    {
        // explorer-context-menu-compress-to is gated to builds 26100+ (24H2); build 22631 is below.
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22631);

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Explorer"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "explorer-context-menu-compress-to", Name = "Compress To" }
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
    public void DetectIncompatibleSettings_CatalogBuildRangeGatedSetting_InRange_ReturnsEmpty()
    {
        // Same 26100+ gated setting, but the build is inside the range.
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(26120);

        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Explorer"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "explorer-context-menu-compress-to", Name = "Compress To" }
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
    public void DetectIncompatibleSettings_CatalogUngatedSetting_ReturnsEmpty()
    {
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22631);

        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Gaming"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem { Id = "gaming-game-mode", Name = "Game Mode" }
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
    public void DetectIncompatibleSettings_UnknownId_IsSkippedSilently()
    {
        // An id with no catalog peer is not a setting - it is neither flagged incompatible nor gated.
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22631);

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
                            new ConfigurationItem { Id = "totally-unknown-id", Name = "Unknown" }
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

    [Fact]
    public void FilterConfigForCurrentSystem_RemovesCatalogIncompatible_KeepsCompatible()
    {
        // On a Win11 build, the Windows10-gated privacy-timeline-suggestions is removed; the ungated
        // gaming-game-mode is kept.
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22631);

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
                            new ConfigurationItem { Id = "gaming-game-mode", Name = "Game Mode" },
                            new ConfigurationItem { Id = "privacy-timeline-suggestions", Name = "Timeline Suggestions" }
                        }
                    }
                }
            }
        };

        var service = CreateService();
        var result = service.FilterConfigForCurrentSystem(config);

        result.Version.Should().Be("2.0");
        result.Optimize.Features["TestFeature"].Items.Should().ContainSingle()
            .Which.Id.Should().Be("gaming-game-mode");
    }

    [Fact]
    public void FilterConfigForCurrentSystem_KeepsUnknownIds()
    {
        _mockWindowsVersionService.Setup(w => w.GetWindowsBuildNumber()).Returns(22631);

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
