using System.Reflection;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

/// <summary>
/// Review state now lives in one overlay object behind one nullable reference, so leaving review is
/// a single assignment rather than a nine-line reset. These tests hold that property: that nothing
/// survives the exit, that a value written outside a review cannot land at all, and — by
/// reflection, so it stays true for review properties nobody has written yet — that dropping the
/// overlay tells the UI about every property that reads from it.
/// </summary>
public class SettingItemViewModelReviewStateTests
{
    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();

    private SettingItemViewModel CreateSut()
    {
        _localizationService.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        _localizationService.MirrorTryGetString();

        var setting = new Setting
        {
            Id = "review-test",
            Display = new() { Name = "Review Test", Description = "d" },
        };

        return new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = setting.Id,
                Name = setting.Display.Name,
                Description = setting.Display.Description,
                InputType = InputType.Toggle,
                IsSelected = false,
            },
            SettingWriteStrategies.Selector(
                _applyService.Object, _dialogService.Object, _localizationService.Object, _logService.Object),
            _logService.Object,
            _dispatcherService.Object,
            _dialogService.Object,
            _localizationService.Object);
    }

    /// <summary>
    /// Every public property on the ViewModel that reads the review overlay, discovered rather than
    /// listed so a review property added later is covered without anyone remembering to come back.
    /// </summary>
    private static IReadOnlyList<PropertyInfo> ReviewProjections() =>
        typeof(SettingItemViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.Contains("Review", StringComparison.Ordinal))
            .Where(p => p.Name != nameof(SettingItemViewModel.IsInReviewMode))
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Where(p => p.Name != nameof(SettingItemViewModel.ReviewActionGroupName))
            .ToList();

    private static SettingItemViewModel WithFullReviewState(SettingItemViewModel vm)
    {
        vm.IsInReviewMode = true;
        vm.HasReviewDiff = true;
        vm.ReviewDiffMessage = "Current: On -> Config: Off";
        vm.IsReviewApproved = true;
        vm.HasReviewAction = true;
        vm.ReviewActionMessage = "Apply the wallpaper";
        vm.IsReviewActionApproved = true;
        return vm;
    }

    // ── The overlay is the state ──

    [Fact]
    public void IsInReviewMode_ReflectsWhetherAnOverlayExists()
    {
        var sut = CreateSut();

        sut.IsInReviewMode.Should().BeFalse();

        sut.IsInReviewMode = true;
        sut.IsInReviewMode.Should().BeTrue();

        sut.IsInReviewMode = false;
        sut.IsInReviewMode.Should().BeFalse();
    }

    [Fact]
    public void ClearReviewState_LeavesEveryReviewPropertyAtItsDefault()
    {
        var sut = WithFullReviewState(CreateSut());

        sut.ClearReviewState();

        sut.IsInReviewMode.Should().BeFalse();
        foreach (var property in ReviewProjections())
        {
            var value = property.GetValue(sut);
            var expected = property.PropertyType.IsValueType
                ? Activator.CreateInstance(property.PropertyType)
                : null;

            value.Should().Be(expected,
                because: $"{property.Name} belongs to the review and must not survive it");
        }
    }

    [Fact]
    public void LeavingAndReenteringReview_StartsFromACleanOverlay()
    {
        var sut = WithFullReviewState(CreateSut());

        sut.ClearReviewState();
        sut.IsInReviewMode = true;

        sut.HasReviewDiff.Should().BeFalse();
        sut.ReviewDiffMessage.Should().BeNull();
        sut.IsReviewApproved.Should().BeFalse();
        sut.IsReviewActionApproved.Should().BeFalse(
            because: "a second import must not inherit the first import's decisions");
    }

    [Fact]
    public void DroppingTheOverlay_NotifiesEveryPropertyThatReadsIt()
    {
        var sut = WithFullReviewState(CreateSut());

        var notified = new List<string>();
        sut.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? string.Empty);

        sut.IsInReviewMode = false;

        foreach (var property in ReviewProjections())
        {
            notified.Should().Contain(property.Name,
                because: $"{property.Name} reads the overlay, so dropping it changes what the card shows");
        }
        notified.Should().Contain(nameof(SettingItemViewModel.IsInReviewMode));
    }

    [Fact]
    public void WritesOutsideAReview_DoNotLand()
    {
        var sut = CreateSut();

        sut.HasReviewDiff = true;
        sut.ReviewDiffMessage = "leaked";
        sut.IsReviewApproved = true;

        sut.HasReviewDiff.Should().BeFalse();
        sut.ReviewDiffMessage.Should().BeNull();
        sut.IsReviewApproved.Should().BeFalse(
            because: "review values belong to a review; one landing outside it is the contamination the overlay prevents");
    }

    // ── Behaviour the old observable properties had, preserved ──

    [Fact]
    public void ApprovingAfterRejecting_ClearsTheRejection()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        sut.IsReviewRejected = true;
        sut.IsReviewApproved = true;

        sut.IsReviewRejected.Should().BeFalse();
        sut.IsReviewApproved.Should().BeTrue();
        sut.IsReviewDecisionMade.Should().BeTrue();
    }

    [Fact]
    public void RejectingAfterApproving_ClearsTheApproval()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        sut.IsReviewApproved = true;
        sut.IsReviewRejected = true;

        sut.IsReviewApproved.Should().BeFalse();
        sut.IsReviewRejected.Should().BeTrue();
    }

    [Fact]
    public void ApprovingAnAction_ClearsTheActionRejection()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        sut.IsReviewActionRejected = true;
        sut.IsReviewActionApproved = true;

        sut.IsReviewActionRejected.Should().BeFalse();
        sut.IsReviewActionApproved.Should().BeTrue();
    }

    [Fact]
    public void ApprovalChange_RaisesReviewApprovalChangedOnce()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        var raised = new List<bool>();
        sut.ReviewApprovalChanged += (_, approved) => raised.Add(approved);

        sut.IsReviewApproved = true;
        sut.IsReviewApproved = true; // no change - must stay silent

        raised.Should().Equal(true);
    }

    [Fact]
    public void ActionApprovalChange_RaisesReviewActionApprovalChangedOnce()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        var raised = new List<bool>();
        sut.ReviewActionApprovalChanged += (_, approved) => raised.Add(approved);

        sut.IsReviewActionApproved = true;
        sut.IsReviewActionApproved = true;

        raised.Should().Equal(true);
    }

    [Fact]
    public void ClearReviewState_DropsSubscribersBeforeTheOverlay()
    {
        var sut = WithFullReviewState(CreateSut());

        bool heardFromAStaleSubscriber = false;
        sut.ReviewApprovalChanged += (_, _) => heardFromAStaleSubscriber = true;
        sut.ReviewActionApprovalChanged += (_, _) => heardFromAStaleSubscriber = true;

        sut.ClearReviewState();

        heardFromAStaleSubscriber.Should().BeFalse(
            because: "the exit must not look like the user changing their decision");
    }

    [Fact]
    public void EnteringReview_ForcesTheExpanderOpenSoChildDiffsAreVisible()
    {
        var sut = CreateSut();
        sut.IsExpanderExpanded = false;

        sut.IsInReviewMode = true;

        sut.IsExpanderExpanded.Should().BeTrue();
    }

    [Fact]
    public void EnteringReview_DisablesTheCard()
    {
        var sut = CreateSut();
        sut.IsEnabled = true;
        sut.ParentIsEnabled = true;

        sut.EffectiveIsEnabled.Should().BeTrue();

        sut.IsInReviewMode = true;
        sut.EffectiveIsEnabled.Should().BeFalse(
            because: "in review the pending decision is accept or reject, not edit");
    }
}
