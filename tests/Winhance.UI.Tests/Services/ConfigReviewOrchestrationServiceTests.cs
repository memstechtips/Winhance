using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigReviewOrchestrationServiceTests : IDisposable
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IConfigReviewModeService> _mockConfigReviewModeService = new();
    private readonly Mock<IConfigReviewDiffService> _mockConfigReviewDiffService = new();
    private readonly Mock<IConfigImportOverlayService> _mockOverlayService = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();
    private readonly Mock<IConfigAppSelectionService> _mockConfigAppSelectionService = new();
    private readonly Mock<IConfigApplicationExecutionService> _mockConfigExecutionService = new();
    private readonly Mock<IConfigLoadService> _mockConfigLoadService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IReviewModeViewModelCoordinator> _mockVmCoordinator = new();

    private ConfigReviewOrchestrationService? _service;

    public ConfigReviewOrchestrationServiceTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));
    }

    private ConfigReviewOrchestrationService CreateService()
    {
        _service = new ConfigReviewOrchestrationService(
            _mockLogService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockConfigReviewModeService.Object,
            _mockConfigReviewDiffService.Object,
            _mockOverlayService.Object,
            _mockConfigImportState.Object,
            _mockConfigAppSelectionService.Object,
            _mockConfigExecutionService.Object,
            _mockConfigLoadService.Object,
            _mockCompatibleSettingsRegistry.Object,
            _mockEventBus.Object,
            _mockVmCoordinator.Object);
        return _service;
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    // -------------------------------------------------------
    // Constructor / Dispose
    // -------------------------------------------------------

    [Fact]
    public void Constructor_SubscribesToReviewModeChanged()
    {
        var service = CreateService();

        // Verify that we subscribed by raising the event
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(false);
        _mockConfigReviewModeService.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

        // When review mode is exited (IsInReviewMode=false), it publishes ReviewModeExitedEvent
        _mockEventBus.Verify(
            e => e.Publish(It.IsAny<ReviewModeExitedEvent>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_UnsubscribesFromReviewModeChanged()
    {
        var service = CreateService();
        service.Dispose();

        // After dispose, raising the event should not cause further calls
        _mockConfigReviewModeService.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

        // The event bus publish count should be 0 (no subscribed handler to trigger it)
        _mockEventBus.Verify(
            e => e.Publish(It.IsAny<ReviewModeExitedEvent>()),
            Times.Never);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var service = CreateService();
        service.Dispose();

        var act = () => service.Dispose();
        act.Should().NotThrow();
    }

    // -------------------------------------------------------
    // ReviewModeChanged handler
    // -------------------------------------------------------

    [Fact]
    public void OnReviewModeChanged_WhenEnteringReviewMode_ReappliesDiffs()
    {
        var service = CreateService();

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

        _mockVmCoordinator.Verify(v => v.ReapplyReviewDiffsToExistingSettings(), Times.Once);
    }

    [Fact]
    public void OnReviewModeChanged_WhenExitingReviewMode_PublishesReviewModeExitedEvent()
    {
        var service = CreateService();

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(false);
        _mockConfigReviewModeService.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

        _mockEventBus.Verify(
            e => e.Publish(It.IsAny<ReviewModeExitedEvent>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // EnterReviewModeAsync
    // -------------------------------------------------------

    [Fact]
    public async Task EnterReviewModeAsync_FiltersIncompatibleSettings()
    {
        var config = new UnifiedConfigurationFile();
        var filteredConfig = new UnifiedConfigurationFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string> { "Incompatible" });

        _mockConfigLoadService
            .Setup(s => s.FilterConfigForCurrentSystem(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(filteredConfig);

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigLoadService.Verify(s => s.FilterConfigForCurrentSystem(config), Times.Once);
    }

    [Fact]
    public async Task EnterReviewModeAsync_ForcesFilterEnabled()
    {
        var config = new UnifiedConfigurationFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockCompatibleSettingsRegistry.Verify(r => r.SetFilterEnabled(true), Times.Once);
    }

    [Fact]
    public async Task EnterReviewModeAsync_EntersReviewModeOnService()
    {
        var config = new UnifiedConfigurationFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigReviewModeService.Verify(
            r => r.EnterReviewModeAsync(config),
            Times.Once);
    }

    [Fact]
    public async Task EnterReviewModeAsync_WithWindowsApps_SelectsApps()
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

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigAppSelectionService.Verify(
            s => s.SelectWindowsAppsFromConfigAsync(It.IsAny<ConfigSection>()),
            Times.Once);
    }

    [Fact]
    public async Task EnterReviewModeAsync_WithExternalApps_SelectsApps()
    {
        var config = new UnifiedConfigurationFile
        {
            ExternalApps = new ConfigSection
            {
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem { Id = "ext1", Name = "Ext 1" }
                }
            }
        };

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigAppSelectionService.Verify(
            s => s.SelectExternalAppsFromConfigAsync(It.IsAny<ConfigSection>()),
            Times.Once);
    }

    [Fact]
    public async Task EnterReviewModeAsync_WithNoApps_DoesNotSelectApps()
    {
        var config = new UnifiedConfigurationFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Returns(new List<string>());

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigAppSelectionService.Verify(
            s => s.SelectWindowsAppsFromConfigAsync(It.IsAny<ConfigSection>()),
            Times.Never);
        _mockConfigAppSelectionService.Verify(
            s => s.SelectExternalAppsFromConfigAsync(It.IsAny<ConfigSection>()),
            Times.Never);
    }

    [Fact]
    public async Task EnterReviewModeAsync_OnException_ExitsReviewModeAndShowsError()
    {
        var config = new UnifiedConfigurationFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<UnifiedConfigurationFile>()))
            .Throws(new Exception("Test error"));

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigReviewModeService.Verify(r => r.ExitReviewMode(), Times.Once);
        _mockDialogService.Verify(
            d => d.ShowMessage(It.Is<string>(s => s.Contains("Test error")), It.IsAny<string>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // ApplyReviewedConfigAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ApplyReviewedConfigAsync_WhenNotInReviewMode_DoesNothing()
    {
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(false);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockConfigExecutionService.Verify(
            e => e.ApplyConfigurationWithOptionsAsync(
                It.IsAny<UnifiedConfigurationFile>(),
                It.IsAny<List<string>>(),
                It.IsAny<ImportOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_WhenActiveConfigIsNull_DoesNothing()
    {
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns((UnifiedConfigurationFile?)null);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockConfigExecutionService.Verify(
            e => e.ApplyConfigurationWithOptionsAsync(
                It.IsAny<UnifiedConfigurationFile>(),
                It.IsAny<List<string>>(),
                It.IsAny<ImportOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_WithNoApprovedDiffs_ShowsNoChangesMessage()
    {
        var config = new UnifiedConfigurationFile();

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(config);
        _mockConfigReviewDiffService.Setup(d => d.GetApprovedDiffs()).Returns(new List<ConfigReviewDiff>());

        _mockVmCoordinator.Setup(v => v.HasSelectedWindowsApps).Returns(false);
        _mockVmCoordinator.Setup(v => v.HasSelectedExternalApps).Returns(false);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockDialogService.Verify(d => d.ShowMessage(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_WithApprovedDiffs_CallsExecutionService()
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
                            new ConfigurationItem { Id = "setting1", Name = "S1" }
                        }
                    }
                }
            }
        };

        var approvedDiffs = new List<ConfigReviewDiff>
        {
            new ConfigReviewDiff
            {
                SettingId = "setting1",
                SettingName = "S1",
                FeatureModuleId = "Privacy",
                IsReviewed = true,
                IsApproved = true,
                InputType = InputType.Toggle
            }
        };

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(config);
        _mockConfigReviewDiffService.Setup(d => d.GetApprovedDiffs()).Returns(approvedDiffs);

        _mockVmCoordinator.Setup(v => v.HasSelectedWindowsApps).Returns(false);
        _mockVmCoordinator.Setup(v => v.HasSelectedExternalApps).Returns(false);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockConfigExecutionService.Verify(
            e => e.ApplyConfigurationWithOptionsAsync(
                It.IsAny<UnifiedConfigurationFile>(),
                It.IsAny<List<string>>(),
                It.IsAny<ImportOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_ExitsReviewModeAfterApplying()
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
                            new ConfigurationItem { Id = "s1", Name = "S1" }
                        }
                    }
                }
            }
        };

        var approvedDiffs = new List<ConfigReviewDiff>
        {
            new ConfigReviewDiff
            {
                SettingId = "s1",
                FeatureModuleId = "Privacy",
                IsReviewed = true,
                IsApproved = true,
                InputType = InputType.Toggle
            }
        };

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(config);
        _mockConfigReviewDiffService.Setup(d => d.GetApprovedDiffs()).Returns(approvedDiffs);

        _mockVmCoordinator.Setup(v => v.HasSelectedWindowsApps).Returns(false);
        _mockVmCoordinator.Setup(v => v.HasSelectedExternalApps).Returns(false);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockConfigReviewModeService.Verify(r => r.ExitReviewMode(), Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_OnException_HidesOverlayAndExitsReviewMode()
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
                            new ConfigurationItem { Id = "s1", Name = "S1" }
                        }
                    }
                }
            }
        };

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(config);
        // GetApprovedDiffs() is called before the try block, so we return a valid result.
        // Then throw inside the try block via _vmCoordinator.HasSelectedWindowsApps.
        _mockConfigReviewDiffService.Setup(d => d.GetApprovedDiffs())
            .Returns(new List<ConfigReviewDiff>());
        _mockVmCoordinator.Setup(v => v.HasSelectedWindowsApps)
            .Throws(new Exception("Test error"));

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockOverlayService.Verify(o => o.HideOverlay(), Times.Once);
        _mockConfigReviewModeService.Verify(r => r.ExitReviewMode(), Times.Once);
    }

    // -------------------------------------------------------
    // CancelReviewModeAsync
    // -------------------------------------------------------

    [Fact]
    public async Task CancelReviewModeAsync_WhenNotInReviewMode_DoesNothing()
    {
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(false);

        var service = CreateService();
        await service.CancelReviewModeAsync();

        _mockConfigReviewModeService.Verify(r => r.ExitReviewMode(), Times.Never);
    }

    [Fact]
    public async Task CancelReviewModeAsync_WhenInReviewMode_ClearsSelectionsAndExits()
    {
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);

        var service = CreateService();
        await service.CancelReviewModeAsync();

        _mockConfigAppSelectionService.Verify(
            s => s.ClearWindowsAppsSelectionAsync(),
            Times.Once);
        _mockVmCoordinator.Verify(v => v.ClearExternalAppSelections(), Times.Once);
        _mockConfigReviewModeService.Verify(r => r.ExitReviewMode(), Times.Once);
    }
}
