using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigApplicationExecutionServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IWindowsVersionService> _mockWindowsVersionService = new();
    private readonly Mock<IConfigurationApplicationBridgeService> _mockBridgeService = new();
    private readonly Mock<IWindowsUIManagementService> _mockWindowsUIManagementService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IConfigImportOverlayService> _mockOverlayService = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();
    private readonly Mock<IConfigAppSelectionService> _mockConfigAppSelectionService = new();
    private readonly Mock<IConfigLoadService> _mockConfigLoadService = new();
    private readonly Mock<IReviewModeViewModelCoordinator> _mockVmCoordinator = new();

    public ConfigApplicationExecutionServiceTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));
    }

    private ConfigApplicationExecutionService CreateService()
    {
        return new ConfigApplicationExecutionService(
            _mockLogService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockWindowsVersionService.Object,
            _mockBridgeService.Object,
            _mockWindowsUIManagementService.Object,
            _mockProcessExecutor.Object,
            _mockOverlayService.Object,
            _mockConfigImportState.Object,
            _mockConfigAppSelectionService.Object,
            _mockConfigLoadService.Object,
            _mockVmCoordinator.Object);
    }

    // -------------------------------------------------------
    // ExecuteConfigImportAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ExecuteConfigImportAsync_WhenNoSections_ShowsNoChangesMessage()
    {
        var config = new UnifiedConfigurationFile();
        var options = new ImportOptions();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        _mockDialogService.Verify(d => d.ShowMessage(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteConfigImportAsync_FiltersIncompatibleSettings()
    {
        var config = new UnifiedConfigurationFile();
        var filteredConfig = new UnifiedConfigurationFile();
        var options = new ImportOptions();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string> { "Incompatible Setting" });

        _mockConfigLoadService
            .Setup(s => s.FilterConfigForCurrentSystem(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(filteredConfig);

        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        _mockConfigLoadService.Verify(
            s => s.FilterConfigForCurrentSystem(config),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteConfigImportAsync_WithWindowsApps_SelectsAppsFromConfig()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "app1", Name = "App 1" }
                }
            }
        };

        var options = new ImportOptions
        {
            ProcessWindowsAppsRemoval = false,
            ProcessWindowsAppsInstallation = false
        };

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        // The config has only WindowsApps but no optimize/customize, so only that section
        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        _mockConfigAppSelectionService.Verify(
            s => s.SelectWindowsAppsFromConfigAsync(It.IsAny<ConfigSection>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteConfigImportAsync_WithRemoval_ConfirmsWithUser()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "app1", Name = "App 1" }
                }
            }
        };

        var options = new ImportOptions { ProcessWindowsAppsRemoval = true };

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        _mockConfigAppSelectionService
            .Setup(s => s.ConfirmWindowsAppsRemovalAsync())
            .ReturnsAsync((false, true));

        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        _mockConfigAppSelectionService.Verify(
            s => s.ConfirmWindowsAppsRemovalAsync(),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteConfigImportAsync_WhenRemovalCancelled_ClearsWindowsAppsSelection()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "app1", Name = "App 1" }
                }
            }
        };

        var options = new ImportOptions { ProcessWindowsAppsRemoval = true };

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        _mockConfigAppSelectionService
            .Setup(s => s.ConfirmWindowsAppsRemovalAsync())
            .ReturnsAsync((false, true));

        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        _mockConfigAppSelectionService.Verify(
            s => s.ClearWindowsAppsSelectionAsync(),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteConfigImportAsync_ShowsAndHidesOverlay()
    {
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
                            new ConfigurationItem { Id = "setting1", Name = "Setting 1" }
                        }
                    }
                }
            }
        };

        var options = new ImportOptions();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        _mockBridgeService
            .Setup(b => b.ApplyConfigurationSectionAsync(
                It.IsAny<ConfigSection>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>>>()))
            .ReturnsAsync(true);

        // Explorer restart
        _mockWindowsUIManagementService
            .Setup(w => w.IsProcessRunning("explorer"))
            .Returns(true);

        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        _mockOverlayService.Verify(o => o.ShowOverlay(It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        _mockOverlayService.Verify(o => o.HideOverlay(), Times.Once);
    }

    [Fact]
    public async Task ExecuteConfigImportAsync_SetsAndClearsConfigImportState()
    {
        var isActiveValues = new List<bool>();

        _mockConfigImportState
            .SetupSet(s => s.IsActive = It.IsAny<bool>())
            .Callback<bool>(v => isActiveValues.Add(v));

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
                            new ConfigurationItem { Id = "s1", Name = "S1" }
                        }
                    }
                }
            }
        };

        var options = new ImportOptions();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        _mockBridgeService
            .Setup(b => b.ApplyConfigurationSectionAsync(
                It.IsAny<ConfigSection>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>>>()))
            .ReturnsAsync(true);

        _mockWindowsUIManagementService
            .Setup(w => w.IsProcessRunning("explorer"))
            .Returns(true);

        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        // Should have been set to true then false
        isActiveValues.Should().Contain(true);
        isActiveValues.Should().Contain(false);
        isActiveValues.Last().Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteConfigImportAsync_OnException_HidesOverlayAndShowsError()
    {
        var config = new UnifiedConfigurationFile();
        var options = new ImportOptions();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Throws(new Exception("Test error"));

        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        _mockOverlayService.Verify(o => o.HideOverlay(), Times.Once);
        _mockDialogService.Verify(
            d => d.ShowMessage(It.Is<string>(s => s.Contains("Test error")), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteConfigImportAsync_WithExternalApps_SelectsAppsFromConfig()
    {
        var config = new UnifiedConfigurationFile
        {
            ExternalApps = new ConfigSection
            {
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "ext1", Name = "ExtApp 1" }
                }
            }
        };

        var options = new ImportOptions
        {
            ProcessExternalAppsInstallation = false,
            ProcessExternalAppsRemoval = false
        };

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        var service = CreateService();
        await service.ExecuteConfigImportAsync(config, options);

        _mockConfigAppSelectionService.Verify(
            s => s.SelectExternalAppsFromConfigAsync(It.IsAny<ConfigSection>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // ApplyConfigurationWithOptionsAsync (public overload)
    // -------------------------------------------------------

    [Fact]
    public async Task ApplyConfigurationWithOptionsAsync_WithEmptyConfig_HandlesGracefully()
    {
        var config = new UnifiedConfigurationFile();
        var selectedSections = new List<string>();
        var options = new ImportOptions();

        _mockWindowsUIManagementService
            .Setup(w => w.IsProcessRunning("explorer"))
            .Returns(true);

        var service = CreateService();

        // Should not throw
        await service.ApplyConfigurationWithOptionsAsync(config, selectedSections, options);

        // Explorer restart still runs
        _mockOverlayService.Verify(
            o => o.UpdateStatus(It.IsAny<string>(), It.IsAny<string?>()),
            Times.AtLeastOnce);
    }
}
