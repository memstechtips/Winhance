using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.EventHandlers;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class TooltipRefreshEventHandlerTests : IDisposable
{
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<ITooltipDataService> _mockTooltipService = new();
    private readonly Mock<IGlobalSettingsRegistry> _mockSettingsRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<ISubscriptionToken> _mockSettingAppliedToken = new();
    private readonly Mock<ISubscriptionToken> _mockFeatureComposedToken = new();

    private Func<SettingAppliedEvent, Task>? _capturedSettingAppliedHandler;
    private Func<FeatureComposedEvent, Task>? _capturedFeatureComposedHandler;

    private readonly TooltipRefreshEventHandler _handler;

    public TooltipRefreshEventHandlerTests()
    {
        _mockEventBus
            .Setup(e => e.SubscribeAsync<SettingAppliedEvent>(It.IsAny<Func<SettingAppliedEvent, Task>>()))
            .Callback<Func<SettingAppliedEvent, Task>>(h => _capturedSettingAppliedHandler = h)
            .Returns(_mockSettingAppliedToken.Object);

        _mockEventBus
            .Setup(e => e.SubscribeAsync<FeatureComposedEvent>(It.IsAny<Func<FeatureComposedEvent, Task>>()))
            .Callback<Func<FeatureComposedEvent, Task>>(h => _capturedFeatureComposedHandler = h)
            .Returns(_mockFeatureComposedToken.Object);

        _handler = new TooltipRefreshEventHandler(
            _mockEventBus.Object,
            _mockTooltipService.Object,
            _mockSettingsRegistry.Object,
            _mockLog.Object);
    }

    public void Dispose()
    {
        _handler.Dispose();
    }

    private static SettingDefinition CreateSetting(
        string id,
        IReadOnlyList<RegistrySetting>? registrySettings = null)
    {
        var setting = new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
        };

        if (registrySettings != null)
            setting = setting with { RegistrySettings = registrySettings };

        return setting;
    }

    private static RegistrySetting CreateRegistrySetting(
        string keyPath = @"HKLM\SOFTWARE\Test",
        string valueName = "TestValue",
        string? compositeStringKey = null) => new()
    {
        KeyPath = keyPath,
        ValueName = valueName,
        ValueType = Microsoft.Win32.RegistryValueKind.DWord,
        CompositeStringKey = compositeStringKey,
    };

    // ---------------------------------------------------------------
    // Constructor subscribes to events
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_SubscribesToSettingAppliedEvent()
    {
        _mockEventBus.Verify(
            e => e.SubscribeAsync<SettingAppliedEvent>(It.IsAny<Func<SettingAppliedEvent, Task>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_SubscribesToFeatureComposedEvent()
    {
        _mockEventBus.Verify(
            e => e.SubscribeAsync<FeatureComposedEvent>(It.IsAny<Func<FeatureComposedEvent, Task>>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Constructor guard clauses
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_NullEventBus_ThrowsArgumentNull()
    {
        var action = () => new TooltipRefreshEventHandler(
            null!,
            _mockTooltipService.Object,
            _mockSettingsRegistry.Object,
            _mockLog.Object);

        action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("eventBus");
    }

    [Fact]
    public void Constructor_NullTooltipDataService_ThrowsArgumentNull()
    {
        var action = () => new TooltipRefreshEventHandler(
            _mockEventBus.Object,
            null!,
            _mockSettingsRegistry.Object,
            _mockLog.Object);

        action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("tooltipDataService");
    }

    [Fact]
    public void Constructor_NullSettingsRegistry_ThrowsArgumentNull()
    {
        var action = () => new TooltipRefreshEventHandler(
            _mockEventBus.Object,
            _mockTooltipService.Object,
            null!,
            _mockLog.Object);

        action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("settingsRegistry");
    }

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNull()
    {
        var action = () => new TooltipRefreshEventHandler(
            _mockEventBus.Object,
            _mockTooltipService.Object,
            _mockSettingsRegistry.Object,
            null!);

        action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("logService");
    }

    // ---------------------------------------------------------------
    // Dispose unsubscribes tokens
    // ---------------------------------------------------------------

    [Fact]
    public void Dispose_UnsubscribesSettingAppliedToken()
    {
        _handler.Dispose();

        _mockSettingAppliedToken.Verify(t => t.Dispose(), Times.AtLeastOnce);
    }

    [Fact]
    public void Dispose_UnsubscribesFeatureComposedToken()
    {
        _handler.Dispose();

        _mockFeatureComposedToken.Verify(t => t.Dispose(), Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------
    // Setting applied triggers tooltip refresh
    // ---------------------------------------------------------------

    [Fact]
    public async Task HandleSettingApplied_SettingFound_RefreshesTooltip()
    {
        var settingDef = CreateSetting("test-setting");
        var tooltipData = new SettingTooltipData
        {
            SettingId = "test-setting",
            DisplayValue = "42",
        };

        _mockSettingsRegistry
            .Setup(r => r.GetSetting("test-setting", null))
            .Returns(settingDef);
        _mockTooltipService
            .Setup(s => s.RefreshTooltipDataAsync("test-setting", settingDef))
            .ReturnsAsync(tooltipData);
        _mockSettingsRegistry
            .Setup(r => r.GetAllSettings())
            .Returns(Array.Empty<ISettingItem>());

        _capturedSettingAppliedHandler.Should().NotBeNull();
        await _capturedSettingAppliedHandler!(new SettingAppliedEvent("test-setting", true));

        _mockTooltipService.Verify(
            s => s.RefreshTooltipDataAsync("test-setting", settingDef),
            Times.Once);
        _mockEventBus.Verify(
            e => e.Publish(It.Is<TooltipUpdatedEvent>(
                evt => evt.SettingId == "test-setting" && evt.TooltipData == tooltipData)),
            Times.Once);
    }

    [Fact]
    public async Task HandleSettingApplied_SettingNotFound_DoesNotPublish()
    {
        _mockSettingsRegistry
            .Setup(r => r.GetSetting("missing", null))
            .Returns((ISettingItem?)null);

        await _capturedSettingAppliedHandler!(new SettingAppliedEvent("missing", true));

        _mockTooltipService.Verify(
            s => s.RefreshTooltipDataAsync(It.IsAny<string>(), It.IsAny<SettingDefinition>()),
            Times.Never);
        _mockEventBus.Verify(
            e => e.Publish(It.IsAny<TooltipUpdatedEvent>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleSettingApplied_RefreshReturnsNull_DoesNotPublishTooltipUpdated()
    {
        var settingDef = CreateSetting("null-tooltip");

        _mockSettingsRegistry
            .Setup(r => r.GetSetting("null-tooltip", null))
            .Returns(settingDef);
        _mockTooltipService
            .Setup(s => s.RefreshTooltipDataAsync("null-tooltip", settingDef))
            .ReturnsAsync((SettingTooltipData?)null);
        _mockSettingsRegistry
            .Setup(r => r.GetAllSettings())
            .Returns(Array.Empty<ISettingItem>());

        await _capturedSettingAppliedHandler!(new SettingAppliedEvent("null-tooltip", true));

        _mockEventBus.Verify(
            e => e.Publish(It.IsAny<TooltipUpdatedEvent>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleSettingApplied_WithCompositeSiblings_RefreshesSiblingTooltips()
    {
        var sharedReg = CreateRegistrySetting(
            keyPath: @"HKLM\SOFTWARE\Shared",
            valueName: "SharedVal",
            compositeStringKey: "composite-key");
        var appliedSetting = CreateSetting("applied", registrySettings: new[] { sharedReg });
        var siblingReg = CreateRegistrySetting(
            keyPath: @"HKLM\SOFTWARE\Shared",
            valueName: "SharedVal",
            compositeStringKey: "composite-sibling");
        var siblingSetting = CreateSetting("sibling", registrySettings: new[] { siblingReg });

        var appliedTooltip = new SettingTooltipData
        {
            SettingId = "applied",
            DisplayValue = "1",
        };
        var siblingTooltip = new SettingTooltipData
        {
            SettingId = "sibling",
            DisplayValue = "2",
        };

        _mockSettingsRegistry
            .Setup(r => r.GetSetting("applied", null))
            .Returns(appliedSetting);
        _mockTooltipService
            .Setup(s => s.RefreshTooltipDataAsync("applied", appliedSetting))
            .ReturnsAsync(appliedTooltip);
        _mockTooltipService
            .Setup(s => s.RefreshTooltipDataAsync("sibling", siblingSetting))
            .ReturnsAsync(siblingTooltip);
        _mockSettingsRegistry
            .Setup(r => r.GetAllSettings())
            .Returns(new ISettingItem[] { appliedSetting, siblingSetting });

        await _capturedSettingAppliedHandler!(new SettingAppliedEvent("applied", true));

        _mockEventBus.Verify(
            e => e.Publish(It.Is<TooltipUpdatedEvent>(evt => evt.SettingId == "applied")),
            Times.Once);
        _mockEventBus.Verify(
            e => e.Publish(It.Is<TooltipUpdatedEvent>(evt => evt.SettingId == "sibling")),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Feature composed triggers bulk tooltip processing
    // ---------------------------------------------------------------

    [Fact]
    public async Task HandleFeatureComposed_PublishesTooltipUpdatedForEachSetting()
    {
        var s1 = CreateSetting("fs1");
        var s2 = CreateSetting("fs2");
        var settings = new[] { s1, s2 };

        var tooltipMap = new Dictionary<string, SettingTooltipData>
        {
            ["fs1"] = new SettingTooltipData { SettingId = "fs1", DisplayValue = "A" },
            ["fs2"] = new SettingTooltipData { SettingId = "fs2", DisplayValue = "B" },
        };

        _mockTooltipService
            .Setup(s => s.GetTooltipDataAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(tooltipMap);

        _capturedFeatureComposedHandler.Should().NotBeNull();
        await _capturedFeatureComposedHandler!(
            new FeatureComposedEvent("TestModule", settings));

        _mockEventBus.Verify(
            e => e.Publish(It.Is<TooltipUpdatedEvent>(evt => evt.SettingId == "fs1")),
            Times.Once);
        _mockEventBus.Verify(
            e => e.Publish(It.Is<TooltipUpdatedEvent>(evt => evt.SettingId == "fs2")),
            Times.Once);
    }

    [Fact]
    public async Task HandleFeatureComposed_EmptySettings_DoesNotCallService()
    {
        await _capturedFeatureComposedHandler!(
            new FeatureComposedEvent("EmptyModule", Array.Empty<SettingDefinition>()));

        _mockTooltipService.Verify(
            s => s.GetTooltipDataAsync(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.Never);
        _mockEventBus.Verify(
            e => e.Publish(It.IsAny<TooltipUpdatedEvent>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleFeatureComposed_LogsProcessedCount()
    {
        var s1 = CreateSetting("log1");
        var tooltipMap = new Dictionary<string, SettingTooltipData>
        {
            ["log1"] = new SettingTooltipData { SettingId = "log1", DisplayValue = "X" },
        };

        _mockTooltipService
            .Setup(s => s.GetTooltipDataAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(tooltipMap);

        await _capturedFeatureComposedHandler!(
            new FeatureComposedEvent("LogModule", new[] { s1 }));

        _mockLog.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Info,
                It.Is<string>(msg => msg.Contains("1/1") && msg.Contains("LogModule")),
                null),
            Times.Once);
    }

    [Fact]
    public async Task HandleFeatureComposed_ServiceThrows_LogsError()
    {
        var s1 = CreateSetting("err1");

        _mockTooltipService
            .Setup(s => s.GetTooltipDataAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ThrowsAsync(new InvalidOperationException("test failure"));

        await _capturedFeatureComposedHandler!(
            new FeatureComposedEvent("FailModule", new[] { s1 }));

        _mockLog.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Error,
                It.Is<string>(msg => msg.Contains("FailModule") && msg.Contains("test failure")),
                null),
            Times.Once);
    }

    [Fact]
    public async Task HandleSettingApplied_ExceptionThrown_LogsError()
    {
        _mockSettingsRegistry
            .Setup(r => r.GetSetting("exploding", null))
            .Throws(new InvalidOperationException("boom"));

        await _capturedSettingAppliedHandler!(new SettingAppliedEvent("exploding", true));

        _mockLog.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Error,
                It.Is<string>(msg => msg.Contains("exploding") && msg.Contains("boom")),
                null),
            Times.Once);
    }
}
