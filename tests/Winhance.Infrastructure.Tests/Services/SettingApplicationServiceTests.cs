using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SettingApplicationServiceTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _mockSettingsRegistry = new();
    private readonly Mock<ISpecialSettingHandlerRegistry> _mockSpecialHandlerRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IGlobalSettingsRegistry> _mockRegistry = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IRecommendedSettingsApplier> _mockRecommended = new();
    private readonly Mock<IProcessRestartManager> _mockRestart = new();
    private readonly Mock<ISettingDependencyResolver> _mockDepResolver = new();
    private readonly Mock<IWindowsCompatibilityFilter> _mockCompatFilter = new();
    private readonly Mock<ISettingOperationExecutor> _mockExecutor = new();
    private readonly Mock<IChangeHistoryService> _mockChangeHistory = new();
    private readonly Mock<ISystemSettingsDiscoveryService> _mockDiscovery = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly SettingApplicationService _service;

    public SettingApplicationServiceTests()
    {
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.IsAny<SettingDefinition>(), It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Default: discovery finds nothing (no before-state), and GetString echoes the key back.
        // A key-echo is NOT the "[{key}]" miss-marker, so by default ResolveLocalized treats every
        // key as a HIT returning the key text; tests that assert on display strings set explicit returns.
        _mockDiscovery
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _service = new SettingApplicationService(
            _mockSettingsRegistry.Object, _mockSpecialHandlerRegistry.Object,
            _mockLog.Object, _mockRegistry.Object,
            _mockEventBus.Object, _mockRecommended.Object, _mockRestart.Object,
            _mockDepResolver.Object, _mockCompatFilter.Object, _mockExecutor.Object,
            _mockChangeHistory.Object, _mockDiscovery.Object, _mockLocalization.Object);
    }

    private static SettingDefinition CreateSetting(string id) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
    };

    private void SetupSettingInRegistry(string settingId, string featureId = "TestDomain")
    {
        var setting = CreateSetting(settingId);
        _mockSettingsRegistry.Setup(r => r.GetById(settingId)).Returns(setting);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting(settingId)).Returns(featureId);
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings(featureId))
            .Returns(new[] { setting });
    }

    [Fact]
    public async Task ApplySettingAsync_ValidSetting_ReturnsSuccess()
    {
        SetupSettingInRegistry("test-setting");

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplySettingAsync_ValidSetting_PublishesEvent()
    {
        SetupSettingInRegistry("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockEventBus.Verify(e => e.Publish(It.Is<SettingAppliedEvent>(
            evt => evt.SettingId == "test-setting")), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_ValidSetting_CallsOperationExecutor()
    {
        SetupSettingInRegistry("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockExecutor.Verify(e => e.ApplySettingOperationsAsync(
            It.Is<SettingDefinition>(s => s.Id == "test-setting"),
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_SettingNotFound_ThrowsArgumentException()
    {
        _mockSettingsRegistry.Setup(r => r.GetById("missing"))
            .Returns((SettingDefinition?)null);

        var action = () => _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "missing",
            Enable = true,
        });

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*missing*not found*");
    }

    [Fact]
    public async Task ApplySettingAsync_RegistersSettingInGlobalRegistry()
    {
        SetupSettingInRegistry("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockRegistry.Verify(r => r.RegisterSetting("TestDomain",
            It.Is<SettingDefinition>(s => s.Id == "test-setting")), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_HandlesDependencies()
    {
        SetupSettingInRegistry("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockDepResolver.Verify(d => d.HandleDependenciesAsync(
            "test-setting",
            It.IsAny<IEnumerable<SettingDefinition>>(),
            true,
            null,
            _service), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_SkipValuePrerequisites_SkipsDependencies()
    {
        SetupSettingInRegistry("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
            SkipValuePrerequisites = true,
        });

        _mockDepResolver.Verify(d => d.HandleDependenciesAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<SettingDefinition>>(),
            It.IsAny<bool>(),
            It.IsAny<object?>(),
            It.IsAny<ISettingApplicationService>()), Times.Never);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForFeatureAsync_DelegatesToApplier()
    {
        await _service.ApplyRecommendedSettingsForFeatureAsync("test-id");

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            "test-id", _service), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_ActionWithApplyRecommended_OneCoalescedRestartForPrimaryPlusRecommended()
    {
        // Bug A "one restart per click": the primary Action apply and the recommended batch must
        // run inside a single SuppressRestarts() scope and produce exactly ONE coalesced restart
        // covering the primary action AND every recommended setting.
        var actionSetting = new SettingDefinition
        {
            Id = "action-clean",
            Name = "Action Clean",
            Description = "desc",
            InputType = InputType.Action,
        };
        _mockSettingsRegistry.Setup(r => r.GetById("action-clean")).Returns(actionSetting);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting("action-clean")).Returns("TestDomain");
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings("TestDomain")).Returns(new[] { actionSetting });

        var recommended = new SettingDefinition { Id = "rec1", Name = "Rec1", Description = "d" };
        _mockRecommended
            .Setup(r => r.ApplyRecommendedForFeatureAsync("action-clean", It.IsAny<ISettingApplicationService>()))
            .ReturnsAsync(new List<SettingDefinition> { recommended });

        // The using-scope needs a real IDisposable back from the mock.
        _mockRestart.Setup(r => r.SuppressRestarts()).Returns(Mock.Of<IDisposable>());

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "action-clean",
            Enable = true,
            ApplyRecommended = true,
        });

        // One suppress scope wraps both the primary action and the recommended batch.
        _mockRestart.Verify(r => r.SuppressRestarts(), Times.Once);

        // The recommended batch runs through the NON-flushing feature core...
        _mockRecommended.Verify(r => r.ApplyRecommendedForFeatureAsync(
            "action-clean", _service), Times.Once);
        // ...and the standalone flushing entry is NOT used on this path (would double-restart).
        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);

        // Exactly one coalesced flush, containing the primary action AND the recommended setting.
        _mockRestart.Verify(r => r.FlushCoalescedRestartsAsync(
            It.Is<IEnumerable<SettingDefinition>>(list =>
                list.Any(s => s.Id == "action-clean") && list.Any(s => s.Id == "rec1"))),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // BP-1: Failure propagation from OperationExecutor
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingAsync_ExecutorFails_PropagatesFailedResult()
    {
        SetupSettingInRegistry("fail-setting");
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.Is<SettingDefinition>(s => s.Id == "fail-setting"),
                It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Failed("Registry write denied"));

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "fail-setting",
            Enable = true,
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Registry write denied");
    }

    [Fact]
    public async Task ApplySettingAsync_ExecutorFails_StillPublishesEvent()
    {
        SetupSettingInRegistry("fail-event");
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.Is<SettingDefinition>(s => s.Id == "fail-event"),
                It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Failed("Some failure"));

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "fail-event",
            Enable = true,
        });

        _mockEventBus.Verify(e => e.Publish(It.Is<SettingAppliedEvent>(
            evt => evt.SettingId == "fail-event")), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_ExecutorSucceeds_ReturnsSuccess()
    {
        SetupSettingInRegistry("ok-setting");
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.Is<SettingDefinition>(s => s.Id == "ok-setting"),
                It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "ok-setting",
            Enable = true,
        });

        result.Success.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Change history (#367): record setting changes before → after
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingAsync_ToggleSuccess_LogsChangeHistoryEntry()
    {
        SetupSettingInRegistry("toggle-setting");

        // Before-state: discovery reports the toggle currently disabled.
        _mockDiscovery
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["toggle-setting"] = new SettingStateResult { Success = true, IsEnabled = false },
            });

        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_0")).Returns("Disabled");
        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_1")).Returns("Enabled");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "toggle-setting",
            Enable = true,
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), "Disabled", "Enabled"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_BeforeEqualsAfter_DoesNotLog()
    {
        SetupSettingInRegistry("noop-setting");

        // Before-state already matches the requested state (enabled → enable=true).
        _mockDiscovery
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["noop-setting"] = new SettingStateResult { Success = true, IsEnabled = true },
            });

        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_0")).Returns("Disabled");
        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_1")).Returns("Enabled");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "noop-setting",
            Enable = true,
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_ChangeHistoryThrows_ApplyStillSucceeds()
    {
        SetupSettingInRegistry("throwing-setting");

        _mockDiscovery
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["throwing-setting"] = new SettingStateResult { Success = true, IsEnabled = false },
            });

        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_0")).Returns("Disabled");
        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_1")).Returns("Enabled");

        _mockChangeHistory
            .Setup(h => h.LogSettingChange(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("history write blew up"));

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "throwing-setting",
            Enable = true,
        });

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplySettingAsync_OperationFails_DoesNotLogChangeHistory()
    {
        SetupSettingInRegistry("fail-no-history");
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.Is<SettingDefinition>(s => s.Id == "fail-no-history"),
                It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Failed("denied"));

        _mockDiscovery
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["fail-no-history"] = new SettingStateResult { Success = true, IsEnabled = false },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "fail-no-history",
            Enable = true,
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockChangeHistory.Verify(h => h.LogSettingAction(
            It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }
}
