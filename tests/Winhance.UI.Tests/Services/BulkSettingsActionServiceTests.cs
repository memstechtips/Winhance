using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.Services;

/// <summary>
/// Tests for <see cref="BulkSettingsActionService"/> Apply Recommended / Reset to Default on
/// Selection settings, plus round-trip agreement with <see cref="SettingItemViewModel"/> badge
/// state. These lock in the wiring that reads Recommended/WindowsDefault roles from the catalog
/// Setting's States.
/// </summary>
public class BulkSettingsActionServiceTests
{
    private const string TestSettingId = "test-selection";

    private readonly Mock<ICatalogSettingsRegistry> _settingsRegistry = new();
    private readonly Mock<IWindowsVersionService> _versionService = new();
    private readonly Mock<ISettingApplicationService> _applicationService = new();
    private readonly Mock<IProcessRestartManager> _processRestartManager = new();
    private readonly Mock<IRecommendedSettingsApplier> _recommendedApplier = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IChangeHistoryService> _changeHistoryService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();

    public BulkSettingsActionServiceTests()
    {
        _versionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22631);
        _versionService.Setup(v => v.GetWindowsBuildRevision()).Returns(0);
        _versionService.Setup(v => v.IsWindows11()).Returns(true);

        _changeHistoryService
            .Setup(h => h.BeginBatch(It.IsAny<string>()))
            .Returns(Mock.Of<IDisposable>());

        _localizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string k) => k);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _localizationService.MirrorTryGetString();
    }

    private BulkSettingsActionService CreateSut(params Setting[] settings)
    {
        foreach (var s in settings)
            // The registry returns the catalog Setting directly: the round-trip reads its States + roles.
            _settingsRegistry.Setup(r => r.GetById(s.Id, It.IsAny<bool>())).Returns(s);

        _processRestartManager
            .Setup(p => p.SuppressRestarts())
            .Returns(Mock.Of<System.IDisposable>());
        _processRestartManager
            .Setup(p => p.FlushCoalescedRestartsAsync(It.IsAny<System.Collections.Generic.IEnumerable<Setting>>()))
            .Returns(Task.CompletedTask);

        // Applier mock: delegates each setting's apply to _applicationService so
        // existing round-trip tests that capture the written Value still work.
        _recommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<System.Collections.Generic.IReadOnlyList<Setting>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<System.IProgress<TaskProgressDetail>>()))
            .Returns(async (System.Collections.Generic.IReadOnlyList<Setting> list,
                            ISettingApplicationService applySvc,
                            System.IProgress<TaskProgressDetail> _) =>
            {
                var applied = new System.Collections.Generic.List<Setting>();
                foreach (var s in list)
                {
                    if (s.Control == ControlKind.Selection)
                    {
                        // Find the Recommended-role state index.
                        int recIdx = -1;
                        for (int i = 0; i < s.States.Count; i++)
                            if (s.States[i].HasRole(RoleKind.Recommended)) { recIdx = i; break; }
                        if (recIdx >= 0)
                        {
                            await applySvc.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = s.Id,
                                Enable = true,
                                Value = recIdx,
                                SkipValuePrerequisites = true,
                            });
                            applied.Add(s);
                        }
                    }
                    // Other types: skip (not needed by existing UI tests)
                }
                return (System.Collections.Generic.IReadOnlyList<Setting>)applied;
            });

        return new BulkSettingsActionService(
            _settingsRegistry.Object,
            _versionService.Object,
            _applicationService.Object,
            _processRestartManager.Object,
            _recommendedApplier.Object,
            _logService.Object,
            _changeHistoryService.Object,
            _localizationService.Object);
    }

    // A synthetic catalog Selection Setting: 3 states A/B/C carrying the Recommended/WindowsDefault roles.
    // The bulk service + round-trip badge read only Control + roles.
    private static Setting MakeSelectionSetting(
        int? recommendedIndex,
        int defaultIndex,
        string id = TestSettingId)
    {
        var states = new List<SettingState>();
        for (int i = 0; i < 3; i++)
        {
            var roles = new List<StateRole>();
            if (recommendedIndex == i) roles.Add(StateRole.Recommended);
            if (defaultIndex == i) roles.Add(StateRole.WindowsDefault);
            states.Add(new SettingState { Label = ((char)('A' + i)).ToString(), Roles = roles });
        }
        return new Setting
        {
            Id = id,
            Display = new() { Name = "Test", Description = "" },
            States = states,
        };
    }

    // ── Apply / Reset: direct service behavior ──

    [Fact]
    public async Task ApplyRecommended_Selection_WritesRecommendedIndex()
    {
        var setting = MakeSelectionSetting(recommendedIndex: 1, defaultIndex: 0);
        var sut = CreateSut(setting);

        var applied = await sut.ApplyRecommendedAsync(new[] { setting.Id });

        applied.Should().Be(1);
        _applicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == TestSettingId &&
                r.Value != null &&
                (int)r.Value == 1 &&
                r.Enable == true &&
                r.ResetToDefault == false)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyRecommended_Selection_NoRecommendedOption_Skips()
    {
        // Informational ComboBox case: no option flagged IsRecommended.
        var setting = MakeSelectionSetting(recommendedIndex: null, defaultIndex: 0);
        var sut = CreateSut(setting);

        await sut.ApplyRecommendedAsync(new[] { setting.Id });

        _applicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == TestSettingId)),
            Times.Never);
    }

    [Fact]
    public async Task ResetToDefault_Selection_WritesDefaultIndex()
    {
        var setting = MakeSelectionSetting(recommendedIndex: 1, defaultIndex: 0);
        var sut = CreateSut(setting);

        var applied = await sut.ResetToDefaultsAsync(new[] { setting.Id });

        applied.Should().Be(1);
        _applicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == TestSettingId &&
                r.Value != null &&
                (int)r.Value == 0 &&
                r.Enable == true &&
                r.ResetToDefault == true)),
            Times.Once);
    }

    // ── Round-trip: apply service + SettingItemViewModel.ComputeBadgeState() agree ──

    [Fact]
    public async Task ApplyRecommended_Selection_RoundTrip_ViewModelShowsRecommendedBadge()
    {
        var setting = MakeSelectionSetting(recommendedIndex: 1, defaultIndex: 0);
        var sut = CreateSut(setting);

        // Capture the Value the apply service writes.
        object? writtenValue = null;
        _applicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .Callback<ApplySettingRequest>(r => writtenValue = r.Value)
            .ReturnsAsync(OperationResult.Succeeded());

        await sut.ApplyRecommendedAsync(new[] { setting.Id });

        writtenValue.Should().NotBeNull();
        var writtenIndex = (int)writtenValue!;

        // Simulate the UI reflecting the apply: the VM's SelectedValue is the applied option index.
        var vm = CreateSettingItemViewModel(setting);
        vm.SelectedValue = writtenIndex;
        vm.ComputeBadgeState();

        vm.BadgeRow.Should().Contain(
            p => p.Kind == SettingBadgeKind.Recommended && p.IsHighlighted,
            because: "ApplyRecommended wrote the IsRecommended option index, and ComputeBadgeState " +
                     "must agree that the effective selection matches Recommended.");
    }

    [Fact]
    public async Task ResetToDefault_Selection_RoundTrip_ViewModelShowsDefaultBadge()
    {
        var setting = MakeSelectionSetting(recommendedIndex: 1, defaultIndex: 0);
        var sut = CreateSut(setting);

        object? writtenValue = null;
        _applicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .Callback<ApplySettingRequest>(r => writtenValue = r.Value)
            .ReturnsAsync(OperationResult.Succeeded());

        await sut.ResetToDefaultsAsync(new[] { setting.Id });

        writtenValue.Should().NotBeNull();
        var writtenIndex = (int)writtenValue!;

        var vm = CreateSettingItemViewModel(setting);
        vm.SelectedValue = writtenIndex;
        vm.ComputeBadgeState();

        vm.BadgeRow.Should().Contain(
            p => p.Kind == SettingBadgeKind.Default && p.IsHighlighted,
            because: "ResetToDefault wrote the IsDefault option index, and ComputeBadgeState " +
                     "must agree that the effective selection matches Default.");
    }

    // ── SettingItemViewModel construction helper (mirrors SettingItemViewModelTests) ──

    private static SettingItemViewModel CreateSettingItemViewModel(Setting setting)
    {
        var dispatcher = new Mock<IDispatcherService>();
        dispatcher.Setup(d => d.RunOnUIThread(It.IsAny<System.Action>()))
            .Callback<System.Action>(a => a());
        dispatcher.Setup(d => d.RunOnUIThreadAsync(It.IsAny<System.Func<Task>>()))
            .Returns<System.Func<Task>>(f => f());

        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string _) => null!);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        localization.MirrorTryGetString();

        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = InputType.Selection,
            IsSelected = false,
        };

        return new SettingItemViewModel(
            config,
            new Mock<ISettingApplicationService>().Object,
            new Mock<ILogService>().Object,
            dispatcher.Object,
            new Mock<IDialogService>().Object,
            localization.Object,
            new Mock<IEventBus>().Object,
            new Mock<IUserPreferencesService>().Object,
            new Mock<IRegeditLauncher>().Object);
    }
}
