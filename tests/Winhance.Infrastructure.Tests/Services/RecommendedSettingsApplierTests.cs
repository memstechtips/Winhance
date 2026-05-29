using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class RecommendedSettingsApplierTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _mockRegistry = new();
    private readonly Mock<IWindowsVersionService> _mockVersionService = new();
    private readonly Mock<IProcessRestartManager> _mockProcessRestartManager = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<ISettingApplicationService> _mockAppService = new();
    private readonly RecommendedSettingsApplier _applier;

    public RecommendedSettingsApplierTests()
    {
        // Default OS: Windows 11 build 22621
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22621);
        _mockVersionService.Setup(v => v.GetWindowsBuildRevision()).Returns(0);

        // SuppressRestarts returns a real no-op disposable
        _mockProcessRestartManager
            .Setup(p => p.SuppressRestarts())
            .Returns(Mock.Of<IDisposable>());

        _mockProcessRestartManager
            .Setup(p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .Returns(Task.CompletedTask);

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        _applier = new RecommendedSettingsApplier(
            _mockRegistry.Object,
            _mockVersionService.Object,
            _mockProcessRestartManager.Object,
            _mockLog.Object);
    }

    // ── Helpers ──

    private static SettingDefinition CreateToggleSetting(
        string id,
        object? recommendedValue,
        object?[]? enabledValue = null,
        object?[]? disabledValue = null) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        InputType = InputType.Toggle,
        RegistrySettings = new[]
        {
            new RegistrySetting
            {
                KeyPath = @"HKLM\Software\Test",
                ValueName = "TestValue",
                ValueType = RegistryValueKind.DWord,
                RecommendedValue = recommendedValue,
                EnabledValue = enabledValue ?? (recommendedValue != null ? [recommendedValue] : null),
                DisabledValue = disabledValue,
                DefaultValue = null,
            }
        }
    };

    /// <summary>
    /// Creates a Selection setting with ComboBox options, one of which is marked IsRecommended.
    /// Options are stored in the order provided (index = position in list).
    /// </summary>
    private static SettingDefinition CreateSelectionSetting(
        string id,
        int recommendedOptionIndex,
        int numOptions = 3)
    {
        var options = Enumerable.Range(0, numOptions)
            .Select(i => new ComboBoxOption
            {
                DisplayName = $"Option{i}",
                IsRecommended = i == recommendedOptionIndex,
                ValueMappings = new Dictionary<string, object?> { { "TestValue", i } },
            })
            .ToList();

        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata { Options = options },
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Software\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                    IsPrimary = true,
                    RecommendedValue = null,
                    DefaultValue = null,
                }
            }
        };
    }

    private void SetupFeatureLookup(
        string triggerSettingId,
        IEnumerable<SettingDefinition> featureSettings,
        string featureId = "TestFeature")
    {
        _mockRegistry
            .Setup(r => r.GetFeatureIdForSetting(triggerSettingId))
            .Returns(featureId);
        _mockRegistry
            .Setup(r => r.GetFilteredSettings(featureId))
            .Returns(featureSettings);
    }

    // ──────────────────────────────────────────────────────────────────
    // (a) ApplyRecommendedToSettingsAsync — calls ApplySettingAsync per
    //     recommended setting and returns the applied list.
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_CallsApplyPerSetting_ReturnsAppliedList()
    {
        // Arrange: two toggle settings with recommended values
        var setting1 = CreateToggleSetting("toggle-a", recommendedValue: 1, enabledValue: [1]);
        var setting2 = CreateToggleSetting("toggle-b", recommendedValue: 0, enabledValue: [1], disabledValue: [0]);
        var settings = new List<SettingDefinition> { setting1, setting2 };

        // Act
        var result = await _applier.ApplyRecommendedToSettingsAsync(
            settings, _mockAppService.Object);

        // Assert: apply was called for each setting
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "toggle-a" && r.SkipValuePrerequisites == true
        )), Times.Once);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "toggle-b" && r.SkipValuePrerequisites == true
        )), Times.Once);

        // Assert: returned list contains both applied settings
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Id == "toggle-a");
        result.Should().Contain(s => s.Id == "toggle-b");
    }

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_EmptyList_ReturnsEmptyApplied()
    {
        var result = await _applier.ApplyRecommendedToSettingsAsync(
            new List<SettingDefinition>(), _mockAppService.Object);

        result.Should().BeEmpty();
        _mockAppService.Verify(
            s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // (b) Regression Bug-B: Selection with IsRecommended option IS applied
    //     with Value = the option index.
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_SelectionWithIsRecommended_AppliesWithIndex()
    {
        // Arrange: 3 options, recommended at index 2
        var selectionSetting = CreateSelectionSetting("selection-id", recommendedOptionIndex: 2, numOptions: 3);
        var settings = new List<SettingDefinition> { selectionSetting };

        // Act
        var result = await _applier.ApplyRecommendedToSettingsAsync(
            settings, _mockAppService.Object);

        // Assert: Apply called with Value = 2 (the recommended option index) — Bug-B regression
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "selection-id" &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(2) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_SelectionWithNoIsRecommended_IsSkipped()
    {
        // Arrange: all options have IsRecommended=false
        var options = new[]
        {
            new ComboBoxOption { DisplayName = "A", IsRecommended = false, ValueMappings = new Dictionary<string, object?> { {"V", 0} } },
            new ComboBoxOption { DisplayName = "B", IsRecommended = false, ValueMappings = new Dictionary<string, object?> { {"V", 1} } },
        };
        var setting = new SettingDefinition
        {
            Id = "sel-no-rec",
            Name = "No Rec",
            Description = "",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata { Options = options },
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Software\Test", ValueName = "V",
                    ValueType = RegistryValueKind.DWord, IsPrimary = true,
                }
            }
        };

        var result = await _applier.ApplyRecommendedToSettingsAsync(
            new List<SettingDefinition> { setting }, _mockAppService.Object);

        _mockAppService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "sel-no-rec")),
            Times.Never);
        result.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────
    // (c) SuppressRestarts is used; FlushCoalescedRestartsAsync is NOT
    //     called by ApplyRecommendedToSettingsAsync (core never flushes).
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_OpensSuppressScope_DoesNotFlush()
    {
        var setting = CreateToggleSetting("no-flush", recommendedValue: 1, enabledValue: [1]);

        await _applier.ApplyRecommendedToSettingsAsync(
            new List<SettingDefinition> { setting }, _mockAppService.Object);

        // SuppressRestarts must have been called once (the using scope)
        _mockProcessRestartManager.Verify(p => p.SuppressRestarts(), Times.Once);

        // FlushCoalescedRestartsAsync must NOT be called by the core
        _mockProcessRestartManager.Verify(
            p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // (d) ApplyRecommendedSettingsForFeatureAsync DOES call
    //     FlushCoalescedRestartsAsync exactly once.
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyRecommendedSettingsForFeatureAsync_FlushesExactlyOnce()
    {
        // Arrange: feature with one compatible setting
        const string triggerId = "trigger-setting";
        var otherSetting = CreateToggleSetting("other-setting", recommendedValue: 1, enabledValue: [1]);
        SetupFeatureLookup(triggerId, new[] { otherSetting });

        // Act
        await _applier.ApplyRecommendedSettingsForFeatureAsync(triggerId, _mockAppService.Object);

        // Assert: flushed exactly once
        _mockProcessRestartManager.Verify(
            p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForFeatureAsync_ExcludesTriggerSetting()
    {
        // The trigger setting (same ID) must be excluded to prevent self-application / recursion
        const string triggerId = "shared-id";
        var selfSetting  = CreateToggleSetting(triggerId, recommendedValue: 1, enabledValue: [1]);
        var otherSetting = CreateToggleSetting("other-setting", recommendedValue: 1, enabledValue: [1]);
        SetupFeatureLookup(triggerId, new[] { selfSetting, otherSetting });

        await _applier.ApplyRecommendedSettingsForFeatureAsync(triggerId, _mockAppService.Object);

        // Trigger setting itself must not be applied
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == triggerId
        )), Times.Never);

        // Other setting should be applied
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "other-setting"
        )), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForFeatureAsync_UnknownSetting_ThrowsInvalidOperation()
    {
        _mockRegistry
            .Setup(r => r.GetFeatureIdForSetting("unknown-id"))
            .Returns((string?)null);

        var action = () => _applier.ApplyRecommendedSettingsForFeatureAsync("unknown-id", _mockAppService.Object);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unknown-id*");
    }

    // ──────────────────────────────────────────────────────────────────
    // Error resilience: individual setting failure continues the loop
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_IndividualFailure_ContinuesWithRemaining()
    {
        var failSetting    = CreateToggleSetting("fail-setting",    recommendedValue: 1, enabledValue: [1]);
        var succeedSetting = CreateToggleSetting("succeed-setting", recommendedValue: 1, enabledValue: [1]);

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "fail-setting")))
            .ThrowsAsync(new InvalidOperationException("Apply failed"));

        // Act: should not throw despite partial failure
        var result = await _applier.ApplyRecommendedToSettingsAsync(
            new List<SettingDefinition> { failSetting, succeedSetting }, _mockAppService.Object);

        // Succeeded setting is in the result; failed one is not
        result.Should().HaveCount(1);
        result.Should().Contain(s => s.Id == "succeed-setting");

        // Warning logged for the failure
        _mockLog.Verify(l => l.Log(
            LogLevel.Warning,
            It.Is<string>(msg => msg.Contains("fail-setting")),
            null), Times.Once);
    }
}
