using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

// These rules used to write straight into named XAML elements, so nothing could assert them without a XAML host.
public class SectionOverviewItemViewModelTests
{
    private const string SectionKey = "Privacy";
    private const string FeatureId = "privacy";

    private readonly Mock<IConfigReviewBadgeService> _badges = new();
    private readonly Mock<IConfigReviewModeService> _reviewMode = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<ISettingsFeatureViewModel> _feature = new();

    public SectionOverviewItemViewModelTests()
    {
        _feature.Setup(f => f.Settings).Returns(new ObservableCollection<SettingItemViewModel>());
        // Unstubbed TryGetString reports "missing" for every key, which is the fallback path.
        // Tests that care about a specific string stub it explicitly.
    }

    private SectionOverviewItemViewModel CreateSut() => new(
        SectionKey,
        FeatureId,
        "PrivacyIconPath",
        _feature.Object,
        _badges.Object,
        _reviewMode.Object,
        _localization.Object);

    // ── Review badge ──

    [Fact]
    public void OutsideReviewMode_NoReviewBadgeShows_EvenWhenTheFeatureHasDiffs()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(false);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(5);

        var sut = CreateSut();

        sut.IsReviewSuccessBadgeVisible.Should().BeFalse();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void WithUnreviewedDiffs_ShowsThePendingCount_NotTheTotal()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(5);
        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(false);
        _badges.Setup(b => b.GetFeaturePendingDiffCount(FeatureId)).Returns(2);

        var sut = CreateSut();

        sut.IsReviewPendingBadgeVisible.Should().BeTrue();
        sut.IsReviewSuccessBadgeVisible.Should().BeFalse();
        // 2 unreviewed of 5 total — the badge counts what is left to do, not what exists.
        sut.ReviewPendingCount.Should().Be(2);
    }

    [Fact]
    public void WhenFullyReviewed_ShowsTheCheckmarkInsteadOfACount()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(5);
        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(true);

        var sut = CreateSut();

        sut.IsReviewSuccessBadgeVisible.Should().BeTrue();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void InTheConfigWithNoDiffs_ShowsTheCheckmark()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(0);
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(true);

        var sut = CreateSut();

        sut.IsReviewSuccessBadgeVisible.Should().BeTrue();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void AbsentFromTheConfig_ShowsNoBadgeAtAll()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(0);
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(false);

        var sut = CreateSut();

        sut.IsReviewSuccessBadgeVisible.Should().BeFalse();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void EnteringReviewMode_UpdatesTheBadgeWithoutAnyoneCallingRefresh()
    {
        // The point of the rewrite: the card reacts to the service, rather than waiting for a page
        // to remember to push new values into a named element.
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(false);
        var sut = CreateSut();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();

        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(3);
        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(false);
        _badges.Setup(b => b.GetFeaturePendingDiffCount(FeatureId)).Returns(3);
        _reviewMode.Raise(r => r.ReviewModeChanged += null, EventArgs.Empty);

        sut.IsReviewPendingBadgeVisible.Should().BeTrue();
        sut.ReviewPendingCount.Should().Be(3);
    }

    [Fact]
    public void BadgeStateChanged_RecomputesTheBadge()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(4);
        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(false);
        _badges.Setup(b => b.GetFeaturePendingDiffCount(FeatureId)).Returns(4);
        var sut = CreateSut();

        _badges.Setup(b => b.IsFeatureFullyReviewed(FeatureId)).Returns(true);
        _badges.Raise(b => b.BadgeStateChanged += null, EventArgs.Empty);

        sut.IsReviewSuccessBadgeVisible.Should().BeTrue();
        sut.IsReviewPendingBadgeVisible.Should().BeFalse();
    }

    // ── View-menu gating ──

    [Fact]
    public void AFeatureWithNoBadgeData_ShowsNoPills()
    {
        var sut = CreateSut();

        sut.ArePillsVisible.Should().BeFalse();
        sut.IsNewBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void TurningInfoBadgesOff_HidesThePills_WithoutTouchingTheReviewBadge()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(0);
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(true);
        var sut = CreateSut();

        sut.AreInfoBadgesVisible = false;

        sut.ArePillsVisible.Should().BeFalse();
        // A review decision is not optional detail — View > InfoBadges must not hide it.
        sut.IsReviewSuccessBadgeVisible.Should().BeTrue();
    }

    // ── Lifetime ──

    [Fact]
    public void Dispose_UnsubscribesFromTheServices()
    {
        _reviewMode.Setup(r => r.IsInReviewMode).Returns(true);
        _badges.Setup(b => b.GetFeatureDiffCount(FeatureId)).Returns(0);
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(false);
        var sut = CreateSut();

        sut.Dispose();

        // Would flip the badge on if it were still listening.
        _badges.Setup(b => b.IsFeatureInConfig(FeatureId)).Returns(true);
        _badges.Raise(b => b.BadgeStateChanged += null, EventArgs.Empty);

        sut.IsReviewSuccessBadgeVisible.Should().BeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var sut = CreateSut();

        var act = () => { sut.Dispose(); sut.Dispose(); };

        act.Should().NotThrow();
    }

    [Fact]
    public void IdentityIsCarriedForTheTemplateToBindAndNavigateWith()
    {
        var sut = CreateSut();

        sut.SectionKey.Should().Be(SectionKey);
        sut.FeatureId.Should().Be(FeatureId);
        sut.IconResourceKey.Should().Be("PrivacyIconPath");
        sut.Feature.Should().BeSameAs(_feature.Object);
    }
}
