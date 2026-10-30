using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.TestSupport;
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
        _mockLocalizationService.Setup(l => l.GetString("Feature_Privacy_Name")).Returns("Privacy");
        _mockLocalizationService.Setup(l => l.GetString("Feature_StartMenu_Name")).Returns("Start");
        _mockLocalizationService.MirrorTryGetString();
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
    public void DetectIncompatibleSettings_NamesTheSettingsFeature_NotTheFileKey()
    {
        HiddenOnThisMachine("start-recommended-section");

        var config = new WinhanceConfigFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Explorer"] = new ConfigSection
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
            .Which.Should().Be("Setting_start-recommended-section_Name (Start)");
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
            .Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
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
            .Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
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

    private const string ConfigPath = @"C:\Configs\mine.winhance";

    private void FileHolds(string json) =>
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync(ConfigPath, It.IsAny<CancellationToken>())).ReturnsAsync(json);

    private static string Json(WinhanceConfigFile config) => JsonSerializer.Serialize(config, ConfigFileConstants.JsonOptions);

    [Fact]
    public async Task ReadConfigurationAsync_MigratesTheFile()
    {
        FileHolds(Json(new WinhanceConfigFile()));

        var result = await CreateService().ReadConfigurationAsync(ConfigPath);

        result.Should().NotBeNull();
        _mockConfigMigrationService.Verify(m => m.MigrateConfig(result!), Times.Once);
    }

    [Fact]
    public async Task ReadConfigurationAsync_UnsupportedVersion_ReturnsNullWithoutADialog()
    {
        FileHolds(Json(new WinhanceConfigFile { Version = "3.0" }));

        var result = await CreateService().ReadConfigurationAsync(ConfigPath);

        result.Should().BeNull();
        _mockDialogService.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReadEmbeddedConfigurationAsync_MigratesThePreset()
    {
        var result = await CreateService().ReadEmbeddedConfigurationAsync(windowsDefaults: false);

        result.Should().NotBeNull();
        _mockConfigMigrationService.Verify(m => m.MigrateConfig(result!), Times.Once);
    }

    [Fact]
    public async Task LoadAndValidateConfigurationAsync_AcceptedFile_RecordsItsNameAndPath()
    {
        _mockConfigImportState.SetupAllProperties();
        FileHolds(Json(new WinhanceConfigFile()));

        var result = await CreateService().LoadAndValidateConfigurationAsync(ConfigPath);

        result.Should().NotBeNull();
        _mockConfigImportState.Object.SourceName.Should().Be("mine.winhance");
        _mockConfigImportState.Object.SourcePath.Should().Be(ConfigPath);
        _mockConfigMigrationService.Verify(m => m.MigrateConfig(result!), Times.Once);
    }

    [Fact]
    public async Task LoadAndValidateConfigurationAsync_UnsupportedVersion_KeepsThePreviousSourcePath()
    {
        _mockConfigImportState.SetupAllProperties();
        _mockConfigImportState.Object.SourcePath = @"C:\Configs\previous.winhance";
        FileHolds(Json(new WinhanceConfigFile { Version = "3.0" }));

        var result = await CreateService().LoadAndValidateConfigurationAsync(ConfigPath);

        result.Should().BeNull();
        _mockConfigImportState.Object.SourcePath.Should().Be(@"C:\Configs\previous.winhance");
        _mockDialogService.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{ not json")]
    public async Task LoadAndValidateConfigurationAsync_UnreadableFile_KeepsThePreviousSourcePath_AndShowsAnError(string json)
    {
        _mockConfigImportState.SetupAllProperties();
        _mockConfigImportState.Object.SourcePath = @"C:\Configs\previous.winhance";
        FileHolds(json);

        var result = await CreateService().LoadAndValidateConfigurationAsync(ConfigPath);

        result.Should().BeNull();
        _mockConfigImportState.Object.SourcePath.Should().Be(@"C:\Configs\previous.winhance");
        _mockDialogService.Verify(d => d.ShowMessage(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task LoadRecommendedConfigurationAsync_MigratesThePreset_AndClearsTheSourcePath()
    {
        _mockConfigImportState.SetupAllProperties();
        _mockConfigImportState.Object.SourcePath = ConfigPath;

        var result = await CreateService().LoadRecommendedConfigurationAsync();

        result.Should().NotBeNull();
        _mockConfigImportState.Object.SourcePath.Should().BeNull();
        _mockConfigMigrationService.Verify(m => m.MigrateConfig(result!), Times.Once);
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

    // A hand-edited file can hold a null feature, a null item list or no group at all.
    private static WinhanceConfigFile WithNullSections() => new()
    {
        Optimize = new FeatureGroupSection
        {
            Features = new Dictionary<string, ConfigSection>
            {
                ["Privacy"] = null!,
                ["Gaming"] = new ConfigSection { Items = null! },
            },
        },
        Customize = null!,
    };

    [Fact]
    public void FilterConfigForCurrentSystem_DropsNullSections_InsteadOfThrowing()
    {
        var result = CreateService().FilterConfigForCurrentSystem(WithNullSections());

        result.Optimize.Features.Keys.Should().Equal("Gaming");
        result.Optimize.Features["Gaming"].Items.Should().BeEmpty();
        result.Customize.Features.Should().BeEmpty();
    }

    [Fact]
    public void DetectIncompatibleSettings_NullSections_FindNothing()
    {
        CreateService().DetectIncompatibleSettings(WithNullSections()).Should().BeEmpty();
    }

    [Fact]
    public void A_null_item_is_skipped_and_the_items_beside_it_are_still_judged()
    {
        HiddenOnThisMachine("privacy-timeline-suggestions");
        var config = new WinhanceConfigFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection { Items = [null!, new ConfigurationItem { Id = "privacy-timeline-suggestions" }] },
                },
            },
        };
        var service = CreateService();

        service.DetectIncompatibleSettings(config).Should().ContainSingle();
        service.FilterConfigForCurrentSystem(config).Optimize.Features["Privacy"].Items.Should().BeEmpty();
    }

    [Fact]
    public void A_retired_id_is_judged_by_the_setting_it_now_names()
    {
        HiddenOnThisMachine("region-short-date");
        var config = new WinhanceConfigFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Explorer"] = new ConfigSection { Items = [new ConfigurationItem { Id = "explorer-customization-short-date" }] },
                },
            },
        };
        var service = CreateService();

        service.DetectIncompatibleSettings(config).Should().ContainSingle()
            .Which.Should().StartWith(SettingCatalog.Find("region-short-date")!.Display.Name.Value);
        service.FilterConfigForCurrentSystem(config).Customize.Features["Explorer"].Items.Should().BeEmpty();
    }

    [Fact]
    public void ParseConfiguration_MigratesTheBytes()
    {
        var result = CreateService().ParseConfiguration(System.Text.Encoding.UTF8.GetBytes(Json(new WinhanceConfigFile())), ConfigPath);

        result.Should().NotBeNull();
        _mockConfigMigrationService.Verify(m => m.MigrateConfig(result!), Times.Once);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{ not json")]
    public void ParseConfiguration_UnreadableBytes_ReturnNull(string json)
    {
        CreateService().ParseConfiguration(System.Text.Encoding.UTF8.GetBytes(json), ConfigPath).Should().BeNull();
    }

    [Fact]
    public void ParseConfiguration_UnsupportedVersion_ReturnsNullWithoutADialog()
    {
        var result = CreateService().ParseConfiguration(System.Text.Encoding.UTF8.GetBytes(Json(new WinhanceConfigFile { Version = "3.0" })), ConfigPath);

        result.Should().BeNull();
        _mockDialogService.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoadUserBackupConfigurationAsync_UnsupportedVersion_IsRejectedWithADialog()
    {
        _mockConfigImportState.SetupAllProperties();
        _mockConfigImportState.Object.SourcePath = @"C:\Configs\previous.winhance";
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\Test\AppData\Local");
        _mockFileSystemService
            .Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Users\Test\AppData\Local\Winhance\Backup");
        _mockFileSystemService.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>())).Returns([ConfigPath]);
        FileHolds(Json(new WinhanceConfigFile { Version = "3.0" }));

        var result = await CreateService().LoadUserBackupConfigurationAsync();

        result.Should().BeNull();
        _mockConfigImportState.Object.SourcePath.Should().Be(@"C:\Configs\previous.winhance");
        _mockDialogService.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
