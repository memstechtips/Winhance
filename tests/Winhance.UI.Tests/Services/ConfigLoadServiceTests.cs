using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
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
    private readonly Mock<ICatalogSettingsRegistry> _mockRegistry = new();
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
            _mockRegistry.Object,
            _mockConfigMigrationService.Object,
            _mockInteractiveUserService.Object,
            _mockFileSystemService.Object,
            _mockMainWindowProvider.Object,
            _mockConfigImportState.Object);
    }

    public ConfigLoadServiceTests()
    {
        _mockRegistry
            .Setup(r => r.GetById(It.IsAny<string>(), It.IsAny<CatalogScope>()))
            .Returns((string id, CatalogScope _) => SettingCatalog.Find(id));
    }

    private void HiddenOnThisMachine(string settingId) =>
        _mockRegistry
            .Setup(r => r.GetById(settingId, CatalogScope.CurrentMachine))
            .Returns((Setting?)null);

    [Fact]
    public void DetectIncompatibleSettings_WithEmptyConfig_ReturnsEmptyList()
    {
        var service = CreateService();
        var config = new WinhanceConfigFile();

        var result = service.DetectIncompatibleSettings(config);

        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectIncompatibleSettings_NamesASettingThisMachineDoesNotShow_WithItsFeature()
    {
        HiddenOnThisMachine("privacy-timeline-suggestions");

        var config = new WinhanceConfigFile
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

        // The service localizes the catalog's key, and with an unstubbed mock the fallback is the key itself.
        result.Should().ContainSingle()
            .Which.Should().Be("Setting_privacy-timeline-suggestions_Name (Privacy)");
    }

    [Fact]
    public void DetectIncompatibleSettings_NamesTheFeatureKeyTheFileFiledItUnder()
    {
        HiddenOnThisMachine("start-recommended-section");

        var config = new WinhanceConfigFile
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
            .Which.Should().Be("Setting_start-recommended-section_Name (StartMenu)");
    }

    [Fact]
    public void DetectIncompatibleSettings_ASettingThisMachineShows_IsCompatible()
    {

        var config = new WinhanceConfigFile
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
    public void DetectIncompatibleSettings_UnknownId_IsSkippedWithoutAskingTheRegistry()
    {
        // An id with no catalog peer is not a setting - it is neither flagged incompatible nor gated.

        var config = new WinhanceConfigFile
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
        _mockRegistry.Verify(r => r.GetById(It.IsAny<string>(), It.IsAny<CatalogScope>()), Times.Never);
    }

    [Fact]
    public void DetectIncompatibleSettings_WithNullFeatures_SkipsSection()
    {

        var config = new WinhanceConfigFile
        {
            Optimize = new FeatureGroupSection { Features = new Dictionary<string, ConfigSection>() },
            Customize = new FeatureGroupSection { Features = new Dictionary<string, ConfigSection>() }
        };

        var service = CreateService();
        var result = service.DetectIncompatibleSettings(config);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterConfigForCurrentSystem_DropsWhatThisMachineDoesNotShow_KeepsTheRest()
    {
        HiddenOnThisMachine("privacy-timeline-suggestions");

        var config = new WinhanceConfigFile
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

        var config = new WinhanceConfigFile
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

        var config = new WinhanceConfigFile
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
