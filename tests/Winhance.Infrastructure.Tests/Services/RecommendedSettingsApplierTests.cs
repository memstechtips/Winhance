using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class RecommendedSettingsApplierTests
{
    private readonly Mock<ICatalogSettingsRegistry> _mockRegistry = new();
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

        // Slice 3b: the applied list is catalog Settings, so the flush overload takes IEnumerable<Setting>.
        _mockProcessRestartManager
            .Setup(p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<Setting>>()))
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

    // Slice 3b: the applier consumes catalog Settings DIRECTLY (the SettingCatalog.Find re-pairing is gone),
    // reading Control + the Recommended role + the resolver's Setting overloads. Tests construct synthetic
    // Settings with exactly the shape/roles they exercise - no real catalog id is needed anymore.

    private static Display Disp(string id) => new() { Name = $"Setting {id}", Description = $"Description for {id}" };

    // A toggle Setting (two Enabled/Disabled states). A Recommended role on the Enabled state means
    // "recommend enabling" (CatalogToggleState.GetRecommended returns true).
    private static Setting ToggleWithRecommendedEnabled(string id) => new()
    {
        Id = id,
        Display = Disp(id),
        States = new[]
        {
            new SettingState { Label = "Enabled", Roles = new[] { new StateRole(RoleKind.Recommended) } },
            new SettingState { Label = "Disabled" },
        },
    };

    // A registry-style Selection Setting (>=3 non-Enabled/Disabled states => Control.Selection). A Recommended
    // role sits on the state at recommendedIndex, so GetRecommendedIndex returns that index.
    private static Setting SelectionWithRecommendedIndex(string id, int recommendedIndex, int numOptions = 3)
    {
        var states = new List<SettingState>(numOptions);
        for (int i = 0; i < numOptions; i++)
        {
            states.Add(new SettingState
            {
                Label = $"Option{i}",
                Roles = i == recommendedIndex
                    ? new[] { new StateRole(RoleKind.Recommended) }
                    : Array.Empty<StateRole>(),
            });
        }
        return new Setting { Id = id, Display = Disp(id), States = states };
    }

    // An Action Setting (no states => Control.Action) - excluded from Apply-Recommended.
    private static Setting ActionSetting(string id) => new() { Id = id, Display = Disp(id) };

    private void SetupFeatureLookup(
        string triggerSettingId,
        IReadOnlyList<Setting> featureSettings,
        string featureId = "TestFeature")
    {
        _mockRegistry
            .Setup(r => r.GetFeatureIdForSetting(triggerSettingId))
            .Returns(featureId);
        _mockRegistry
            .Setup(r => r.GetByFeature(featureId, It.IsAny<bool>()))
            .Returns(featureSettings);
    }

    // ------------------------------------------------------------------
    // (a) ApplyRecommendedToSettingsAsync applies each recommended setting
    //     and returns the applied list.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_CallsApplyPerSetting_ReturnsAppliedList()
    {
        var s1 = ToggleWithRecommendedEnabled("toggle-a");
        var s2 = ToggleWithRecommendedEnabled("toggle-b");
        var settings = new List<Setting> { s1, s2 };

        var result = await _applier.ApplyRecommendedToSettingsAsync(settings, _mockAppService.Object);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "toggle-a" && r.SkipValuePrerequisites == true
        )), Times.Once);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "toggle-b" && r.SkipValuePrerequisites == true
        )), Times.Once);

        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Id == "toggle-a");
        result.Should().Contain(s => s.Id == "toggle-b");
    }

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_EmptyList_ReturnsEmptyApplied()
    {
        var result = await _applier.ApplyRecommendedToSettingsAsync(
            new List<Setting>(), _mockAppService.Object);

        result.Should().BeEmpty();
        _mockAppService.Verify(
            s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // (b) Selection with a Recommended role IS applied with Value = the
    //     recommended state index.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_SelectionWithRecommended_AppliesWithIndex()
    {
        var selection = SelectionWithRecommendedIndex("selection-id", recommendedIndex: 2, numOptions: 3);

        var result = await _applier.ApplyRecommendedToSettingsAsync(
            new List<Setting> { selection }, _mockAppService.Object);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "selection-id" &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(2) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_SelectionWithNoRecommended_IsSkipped()
    {
        // A Selection with no Recommended role on any state (and no powercfg target) is skipped.
        var selection = new Setting
        {
            Id = "sel-no-rec",
            Display = Disp("sel-no-rec"),
            States = new[]
            {
                new SettingState { Label = "A" },
                new SettingState { Label = "B" },
                new SettingState { Label = "C" },
            },
        };

        var result = await _applier.ApplyRecommendedToSettingsAsync(
            new List<Setting> { selection }, _mockAppService.Object);

        _mockAppService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "sel-no-rec")),
            Times.Never);
        result.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // (c) SuppressRestarts is used; FlushCoalescedRestartsAsync is NOT
    //     called by ApplyRecommendedToSettingsAsync (core never flushes).
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_OpensSuppressScope_DoesNotFlush()
    {
        var setting = ToggleWithRecommendedEnabled("no-flush");

        await _applier.ApplyRecommendedToSettingsAsync(
            new List<Setting> { setting }, _mockAppService.Object);

        _mockProcessRestartManager.Verify(p => p.SuppressRestarts(), Times.Once);
        _mockProcessRestartManager.Verify(
            p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<Setting>>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // (d) ApplyRecommendedSettingsForFeatureAsync DOES flush exactly once.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedSettingsForFeatureAsync_FlushesExactlyOnce()
    {
        const string triggerId = "trigger-setting";
        var other = ToggleWithRecommendedEnabled("other-setting");
        SetupFeatureLookup(triggerId, new[] { other });

        await _applier.ApplyRecommendedSettingsForFeatureAsync(triggerId, _mockAppService.Object);

        _mockProcessRestartManager.Verify(
            p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<Setting>>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForFeatureAsync_ExcludesTriggerSetting()
    {
        // The trigger setting (same id) must be excluded to prevent self-application / recursion. BOTH carry a
        // Recommended role, so an un-excluded trigger WOULD apply - it is the exclusion filter (not a pairing
        // skip) that keeps it from applying.
        const string triggerId = "trigger-setting";
        const string otherId = "other-setting";
        var selfSetting = ToggleWithRecommendedEnabled(triggerId);
        var other = ToggleWithRecommendedEnabled(otherId);
        SetupFeatureLookup(triggerId, new[] { selfSetting, other });

        await _applier.ApplyRecommendedSettingsForFeatureAsync(triggerId, _mockAppService.Object);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == triggerId
        )), Times.Never);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == otherId
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

    // ------------------------------------------------------------------
    // Error resilience: individual setting failure continues the loop.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_IndividualFailure_ContinuesWithRemaining()
    {
        var failSetting = ToggleWithRecommendedEnabled("fail-setting");
        var succeedSetting = ToggleWithRecommendedEnabled("succeed-setting");

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "fail-setting")))
            .ThrowsAsync(new InvalidOperationException("Apply failed"));

        var result = await _applier.ApplyRecommendedToSettingsAsync(
            new List<Setting> { failSetting, succeedSetting }, _mockAppService.Object);

        result.Should().HaveCount(1);
        result.Should().Contain(s => s.Id == "succeed-setting");

        _mockLog.Verify(l => l.Log(
            LogLevel.Warning,
            It.Is<string>(msg => msg.Contains("fail-setting")),
            null), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedToSettingsAsync_ActionSetting_IsExcluded()
    {
        // A one-shot Action (no states) is not a stateful setting to bulk-recommend (Marco 2026-07-08). In the
        // catalog model an Action carries no recommendable state, so Apply-Recommended never applies it - the
        // Control==Action guard makes that exclusion explicit.
        var action = ActionSetting("action-id");

        var result = await _applier.ApplyRecommendedToSettingsAsync(
            new List<Setting> { action }, _mockAppService.Object);

        _mockAppService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "action-id")),
            Times.Never);
        result.Should().BeEmpty();
    }
}
