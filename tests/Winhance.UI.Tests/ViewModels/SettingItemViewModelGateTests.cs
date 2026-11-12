using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingItemViewModelGateTests
{
    private const string ParentId = "gate-parent";

    private static readonly string[] ParentStates = ["Picture", "Solid color", "Slideshow"];

    private readonly Mock<ISettingsLoadingService> _loading = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IDispatcherService> _dispatcher = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<IApplicationModeService> _mode = new();

    public SettingItemViewModelGateTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _localization.MirrorTryGetString();
        _dispatcher.Setup(d => d.RunOnUIThread(It.IsAny<Action>())).Callback<Action>(action => action());
        _dispatcher.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>())).Returns<Func<Task>>(a => a());
        _eventBus.Setup(e => e.Subscribe(It.IsAny<Action<SettingAppliedEvent>>()))
            .Returns(new Mock<ISubscriptionToken>().Object);
        _eventBus.Setup(e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()))
            .Returns(new Mock<ISubscriptionToken>().Object);
        _eventBus.Setup(e => e.SubscribeAsync(It.IsAny<Func<AuthoringModeExitedEvent, Task>>()))
            .Returns(new Mock<ISubscriptionToken>().Object);
        _eventBus.Setup(e => e.Subscribe(It.IsAny<Action<ReviewModeExitedEvent>>()))
            .Returns(new Mock<ISubscriptionToken>().Object);
        _loading.Setup(s => s.RefreshScopeDerivedStateAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .Returns(Task.CompletedTask);
        _mode.Setup(m => m.IsIncluded(It.IsAny<string>())).Returns(true);
    }

    private SettingItemViewModel Card(string id, StateGate? visibleWhen = null, string[]? states = null)
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting
            {
                Id = id,
                Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id) },
                UiParentId = id == ParentId ? null : ParentId,
                VisibleWhen = visibleWhen,
                States = (states ?? []).Select(label => new SettingState { Label = TestKeys.Of(label) }).ToArray(),
            },
            SettingId = id,
            Name = id,
            Description = id,
            GroupName = "Group",
            InputType = InputType.Selection,
        };

        return new SettingItemViewModel(
            config,
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, new Mock<IDialogService>().Object,
                _localization.Object, _log.Object),
            _log.Object,
            _dispatcher.Object,
            new Mock<IDialogService>().Object,
            _localization.Object,
            applicationModeService: _mode.Object);
    }

    private async Task<TestableSettingsFeatureViewModel> FeatureWith(params SettingItemViewModel[] cards)
    {
        _loading
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>(cards));

        var feature = new TestableSettingsFeatureViewModel(
            _loading.Object, _log.Object, _localization.Object, _dispatcher.Object, _eventBus.Object, _mode.Object);
        await feature.LoadSettingsAsync();
        return feature;
    }

    private static StateGate WhenParentIs(string stateLabel) =>
        new(ParentId, [TestKeys.Of(stateLabel)]);

    [Fact]
    public async Task EffectiveIsVisible_WhenGateNamesAnotherParentState_IsFalse()
    {
        var parent = Card(ParentId, states: ParentStates);
        var child = Card("gate-child", WhenParentIs("Slideshow"));
        await FeatureWith(parent, child);

        parent.SelectedValue = 0;

        child.IsVisible.Should().BeTrue(because: "the search filter has taken nothing off the page");
        child.ParentIsVisible.Should().BeFalse();
        child.EffectiveIsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task EffectiveIsVisible_WhenReviewWantsToChangeAGatedChild_IsTrueWhileTheReviewRuns()
    {
        var parent = Card(ParentId, states: ParentStates);
        var child = Card("gate-child", WhenParentIs("Slideshow"));
        await FeatureWith(parent, child);
        parent.SelectedValue = 0;
        child.EffectiveIsVisible.Should().BeFalse();

        child.IsInReviewMode = true;
        child.HasReviewDiff = true;

        child.EffectiveIsVisible.Should().BeTrue(
            because: "Apply waits for every diffed card to be reviewed, and a hidden card never can be");

        child.ClearReviewState();

        child.EffectiveIsVisible.Should().BeFalse(because: "the gate owns the card again once the review ends");
    }

    [Fact]
    public async Task EffectiveIsVisible_WhenParentMovesOntoTheNamedState_IsTrueAgain()
    {
        var parent = Card(ParentId, states: ParentStates);
        var child = Card("gate-child", WhenParentIs("Slideshow"));
        await FeatureWith(parent, child);
        parent.SelectedValue = 0;

        parent.SelectedValue = 2;

        child.EffectiveIsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task EffectiveIsVisible_WithNoDeclaredGate_IsTrue()
    {
        var parent = Card(ParentId, states: ParentStates);
        var child = Card("gate-child");
        await FeatureWith(parent, child);

        parent.SelectedValue = 1;

        child.EffectiveIsVisible.Should().BeTrue(
            because: "nesting under a parent gates nothing on its own - only a declared VisibleWhen does");
    }

    [Fact]
    public async Task IsLastChild_WhenTheLastChildIsGatedOff_FollowsTheLastDrawnChild()
    {
        var parent = Card(ParentId, states: ParentStates);
        var first = Card("gate-first");
        var last = Card("gate-last", WhenParentIs("Slideshow"));
        await FeatureWith(parent, first, last);

        parent.SelectedValue = 0;

        last.EffectiveIsVisible.Should().BeFalse();
        first.IsLastChild.Should().BeTrue(
            because: "the visible stack has to close on the card the user can actually see");
        last.IsLastChild.Should().BeFalse();
    }

    [Fact]
    public async Task IsLastChild_WhenTheHiddenChildReturns_MovesBackToIt()
    {
        var parent = Card(ParentId, states: ParentStates);
        var first = Card("gate-first");
        var last = Card("gate-last", WhenParentIs("Slideshow"));
        await FeatureWith(parent, first, last);
        parent.SelectedValue = 0;

        parent.SelectedValue = 2;

        last.IsLastChild.Should().BeTrue();
        first.IsLastChild.Should().BeFalse();
    }

    [Fact]
    public async Task ParentCornerRadius_WhenNoChildIsOnScreen_RoundsTheParentsBottomCorners()
    {
        var parent = Card(ParentId, states: ParentStates);
        var first = Card("gate-first", WhenParentIs("Slideshow"));
        var last = Card("gate-last", WhenParentIs("Slideshow"));
        await FeatureWith(parent, first, last);

        parent.SelectedValue = 0;

        parent.HasVisibleChildren.Should().BeFalse();
        parent.ParentCornerRadius.BottomLeft.Should().Be(4d);
        parent.ParentCornerRadius.BottomRight.Should().Be(4d);
    }

    [Fact]
    public async Task ParentCornerRadius_WhenAChildIsOnScreen_LeavesTheBottomCornersOpen()
    {
        var parent = Card(ParentId, states: ParentStates);
        var first = Card("gate-first", WhenParentIs("Slideshow"));
        var last = Card("gate-last", WhenParentIs("Slideshow"));
        await FeatureWith(parent, first, last);
        parent.SelectedValue = 0;

        parent.SelectedValue = 2;

        parent.HasVisibleChildren.Should().BeTrue();
        parent.ParentCornerRadius.TopLeft.Should().Be(4d);
        parent.ParentCornerRadius.BottomLeft.Should().Be(0d);
    }

    [Fact]
    public async Task HasVisibleSettings_WhenOnlyCardsAreGatedOff_IsFalse()
    {
        var parent = Card(ParentId, WhenParentIs("Slideshow"), ParentStates);
        var feature = await FeatureWith(parent);

        parent.SelectedValue = 0;

        feature.HasVisibleSettings.Should().BeFalse();
    }
}
