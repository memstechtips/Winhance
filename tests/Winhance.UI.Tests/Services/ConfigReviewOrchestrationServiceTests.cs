using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigReviewOrchestrationServiceTests : IDisposable
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();
    private readonly Mock<IConfigReviewModeService> _mockConfigReviewModeService = new();
    private readonly Mock<IConfigReviewDiffService> _mockConfigReviewDiffService = new();
    private readonly Mock<IConfigImportOverlayService> _mockOverlayService = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();
    private readonly Mock<IConfigAppSelectionService> _mockConfigAppSelectionService = new();
    private readonly Mock<IConfigApplicationExecutionService> _mockConfigExecutionService = new();
    private readonly Mock<IConfigLoadService> _mockConfigLoadService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IReviewModeViewModelCoordinator> _mockVmCoordinator = new();
    private readonly Mock<IPolicyCleanupService> _mockPolicyCleanupService = new();
    private readonly Mock<IChangeHistoryService> _mockChangeHistoryService = new();
    private readonly Mock<IHardwareFilterService> _mockHardwareFilter = new();

    private ConfigReviewOrchestrationService? _service;

    public ConfigReviewOrchestrationServiceTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));

        _mockChangeHistoryService
            .Setup(h => h.BeginBatch(It.IsAny<string>()))
            .Returns(Mock.Of<IDisposable>());

        _mockHardwareFilter.Setup(h => h.ResetAsync()).Returns(Task.CompletedTask);
    }

    private ConfigReviewOrchestrationService CreateService()
    {
        _service = new ConfigReviewOrchestrationService(
            _mockLogService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockApplicationModeService.Object,
            _mockConfigReviewModeService.Object,
            _mockConfigReviewDiffService.Object,
            _mockOverlayService.Object,
            _mockConfigImportState.Object,
            _mockConfigAppSelectionService.Object,
            _mockConfigExecutionService.Object,
            _mockConfigLoadService.Object,
            _mockEventBus.Object,
            _mockVmCoordinator.Object,
            _mockPolicyCleanupService.Object,
            _mockChangeHistoryService.Object,
            _mockHardwareFilter.Object);
        return _service;
    }

    public void Dispose()
    {
        _service?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_SubscribesToReviewModeChanged()
    {
        var service = CreateService();

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(false);
        _mockConfigReviewModeService.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

        _mockEventBus.Verify(
            e => e.Publish(It.IsAny<ReviewModeExitedEvent>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_UnsubscribesFromReviewModeChanged()
    {
        var service = CreateService();
        service.Dispose();

        _mockConfigReviewModeService.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

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

    [Fact]
    public void OnReviewModeChanged_EnteringReviewFromBuilder_SkipsReapplyOfStaleViewModels()
    {
        // Builder leaves authored (un-applied) positions on the loaded settings VMs;
        // reapplying diffs against them would treat builder edits as system truth.
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        var service = CreateService();

        // ReviewModeChanged fires before ModeChanged during review entry, so the
        // orchestrator still considers Builder the previous mode at this point.
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

        _mockVmCoordinator.Verify(v => v.ReapplyReviewDiffsToExistingSettings(), Times.Never);
    }

    [Fact]
    public void OnApplicationModeChanged_BuilderToNormal_PublishesAuthoringModeExitedEvent()
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        var service = CreateService();

        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        _mockEventBus.Verify(e => e.Publish(It.IsAny<AuthoringModeExitedEvent>()), Times.Once);
    }

    [Fact]
    public void OnApplicationModeChanged_LeavingBuilder_ResetsTheHardwareFilter()
    {
        // Authoring for hardware this PC does not have is Builder-only; anywhere else those settings
        // would read as applicable and apply to nothing.
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        var service = CreateService();

        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        _mockHardwareFilter.Verify(h => h.ResetAsync(), Times.Once);
    }

    [Fact]
    public void OnApplicationModeChanged_StayingOutsideAuthoring_LeavesTheHardwareFilterAlone()
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        var service = CreateService();

        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.ConfigReview);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        _mockHardwareFilter.Verify(h => h.ResetAsync(), Times.Never);
    }

    [Fact]
    public void OnApplicationModeChanged_BuilderToConfigReview_PublishesAuthoringModeExitedEvent()
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        var service = CreateService();

        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.ConfigReview);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        _mockEventBus.Verify(e => e.Publish(It.IsAny<AuthoringModeExitedEvent>()), Times.Once);
    }

    [Fact]
    public void OnApplicationModeChanged_NormalToConfigReview_DoesNotPublishAuthoringModeExitedEvent()
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        var service = CreateService();

        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.ConfigReview);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        _mockEventBus.Verify(e => e.Publish(It.IsAny<AuthoringModeExitedEvent>()), Times.Never);
    }

    [Fact]
    public void OnApplicationModeChanged_BuilderTargetSwitch_DoesNotPublishAuthoringModeExitedEvent()
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        var service = CreateService();

        // Builder target switches raise ModeChanged while CurrentMode stays Builder
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        _mockEventBus.Verify(e => e.Publish(It.IsAny<AuthoringModeExitedEvent>()), Times.Never);
    }

    // Derived from ModeCapabilities rather than listing Builder, so a second authoring mode is
    // covered the moment it declares its capabilities - which is the whole reason the publisher
    // asks AuthorsIntent instead of comparing against WinhanceMode.Builder.

    public static TheoryData<WinhanceMode> AuthoringModes() =>
        Modes(authoring: true);

    public static TheoryData<WinhanceMode> NonAuthoringModes() =>
        Modes(authoring: false);

    private static TheoryData<WinhanceMode> Modes(bool authoring)
    {
        var data = new TheoryData<WinhanceMode>();
        foreach (var mode in Enum.GetValues<WinhanceMode>())
        {
            if (ModeCapabilities.For(mode).AuthorsIntent == authoring)
                data.Add(mode);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AuthoringModes))]
    public void OnApplicationModeChanged_LeavingAnyAuthoringMode_PublishesAuthoringModeExitedEvent(WinhanceMode authoringMode)
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(authoringMode);
        var service = CreateService();

        var destination = Enum.GetValues<WinhanceMode>()
            .First(m => !ModeCapabilities.For(m).AuthorsIntent);
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(destination);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        _mockEventBus.Verify(e => e.Publish(It.IsAny<AuthoringModeExitedEvent>()), Times.Once,
            failMessage: $"leaving {authoringMode} leaves authored, un-applied values on screen");
    }

    [Theory]
    [MemberData(nameof(NonAuthoringModes))]
    public void OnApplicationModeChanged_LeavingAModeThatAuthoredNothing_PublishesNoReload(WinhanceMode plainMode)
    {
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(plainMode);
        var service = CreateService();

        var destination = Enum.GetValues<WinhanceMode>().First(m => m != plainMode);
        _mockApplicationModeService.Setup(m => m.CurrentMode).Returns(destination);
        _mockApplicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        _mockEventBus.Verify(e => e.Publish(It.IsAny<AuthoringModeExitedEvent>()), Times.Never,
            failMessage: $"{plainMode} never moved a value without applying it, so nothing is stale");
    }

    [Fact]
    public async Task EnterReviewModeAsync_FiltersIncompatibleSettings()
    {
        var config = new WinhanceConfigFile();
        var filteredConfig = new WinhanceConfigFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<WinhanceConfigFile>()))
            .Returns(new List<string> { "Incompatible" });

        _mockConfigLoadService
            .Setup(s => s.FilterConfigForCurrentSystem(It.IsAny<WinhanceConfigFile>()))
            .Returns(filteredConfig);

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigLoadService.Verify(s => s.FilterConfigForCurrentSystem(config), Times.Once);
    }

    [Fact]
    public async Task EnterReviewModeAsync_WhenEntryFails_ExitsReviewMode()
    {
        var config = new WinhanceConfigFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<WinhanceConfigFile>()))
            .Returns(new List<string>());

        _mockConfigReviewModeService
            .Setup(s => s.EnterReviewModeAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("entry failed"));

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        // A mid-entry failure (thrown from the review-mode service itself, after the config was
        // filtered) must still tear the mode back down.
        _mockConfigReviewModeService.Verify(s => s.ExitReviewMode(), Times.Once);
    }

    [Fact]
    public async Task EnterReviewModeAsync_EntersReviewModeOnService()
    {
        var config = new WinhanceConfigFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<WinhanceConfigFile>()))
            .Returns(new List<string>());

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigReviewModeService.Verify(
            r => r.EnterReviewModeAsync(config, false),
            Times.Once);
    }

    [Fact]
    public async Task EnterReviewModeAsync_WithWindowsApps_SelectsApps()
    {
        var config = new WinhanceConfigFile
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
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<WinhanceConfigFile>()))
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
        var config = new WinhanceConfigFile
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
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<WinhanceConfigFile>()))
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
        var config = new WinhanceConfigFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<WinhanceConfigFile>()))
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
        var config = new WinhanceConfigFile();

        _mockConfigLoadService
            .Setup(s => s.DetectIncompatibleSettings(It.IsAny<WinhanceConfigFile>()))
            .Throws(new Exception("Test error"));
        _mockLocalizationService
            .Setup(l => l.GetString("Config_Review_EnterError", It.IsAny<object[]>()))
            .Returns((string key, object[] args) => $"{key}:{args[0]}");

        var service = CreateService();
        await service.EnterReviewModeAsync(config);

        _mockConfigReviewModeService.Verify(r => r.ExitReviewMode(), Times.Once);
        _mockDialogService.Verify(
            d => d.ShowMessage(It.Is<string>(s => s.Contains("Test error")), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_WhenNotInReviewMode_DoesNothing()
    {
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(false);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockConfigExecutionService.Verify(
            e => e.ApplyConfigurationWithOptionsAsync(
                It.IsAny<WinhanceConfigFile>(),
                It.IsAny<List<string>>(),
                It.IsAny<ImportOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_WhenActiveConfigIsNull_DoesNothing()
    {
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns((WinhanceConfigFile?)null);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockConfigExecutionService.Verify(
            e => e.ApplyConfigurationWithOptionsAsync(
                It.IsAny<WinhanceConfigFile>(),
                It.IsAny<List<string>>(),
                It.IsAny<ImportOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_WithNoApprovedDiffs_ShowsNoChangesMessage()
    {
        var config = new WinhanceConfigFile();

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
                IsApproved = true
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
                It.IsAny<WinhanceConfigFile>(),
                It.IsAny<List<string>>(),
                It.IsAny<ImportOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_ExitsReviewModeAfterApplying()
    {
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
                IsApproved = true
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

    [Fact]
    public async Task CancelReviewModeAsync_WhenNotInReviewMode_DoesNothing()
    {
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(false);

        var service = CreateService();
        await service.CancelReviewModeAsync();

        _mockConfigReviewModeService.Verify(r => r.ExitReviewMode(), Times.Never);
    }

    [Fact]
    public async Task CancelReviewModeAsync_WhenInReviewMode_PreservesSelectionsAndExits()
    {
        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);

        var service = CreateService();
        await service.CancelReviewModeAsync();

        _mockConfigAppSelectionService.Verify(
            s => s.ClearWindowsAppsSelectionAsync(),
            Times.Never);
        _mockConfigReviewModeService.Verify(r => r.ExitReviewMode(), Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_WindowsDefaults_CallsPolicyCleanup()
    {
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
                IsApproved = true
            }
        };

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.IsWindowsDefaults).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(config);
        _mockConfigReviewDiffService.Setup(d => d.GetApprovedDiffs()).Returns(approvedDiffs);
        _mockVmCoordinator.Setup(v => v.HasSelectedWindowsApps).Returns(false);
        _mockVmCoordinator.Setup(v => v.HasSelectedExternalApps).Returns(false);

        _mockDialogService
            .Setup(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockPolicyCleanupService.Verify(p => p.CleanupPolicyKeys(), Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigAsync_NonWindowsDefaults_DoesNotCallPolicyCleanup()
    {
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
                IsApproved = true
            }
        };

        _mockConfigReviewModeService.Setup(r => r.IsInReviewMode).Returns(true);
        _mockConfigReviewModeService.Setup(r => r.IsWindowsDefaults).Returns(false);
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(config);
        _mockConfigReviewDiffService.Setup(d => d.GetApprovedDiffs()).Returns(approvedDiffs);
        _mockVmCoordinator.Setup(v => v.HasSelectedWindowsApps).Returns(false);
        _mockVmCoordinator.Setup(v => v.HasSelectedExternalApps).Returns(false);

        _mockDialogService
            .Setup(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.ApplyReviewedConfigAsync();

        _mockPolicyCleanupService.Verify(p => p.CleanupPolicyKeys(), Times.Never);
    }
}
