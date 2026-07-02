using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Helpers;

/// <summary>
/// Regression spec for the "Only Changes" review-mode filter predicate.
///
/// <para>
/// The Optimize and Customize pages use this helper to decide row visibility while the
/// "Only Changes" toggle is on. Both the Apply-button gate and the <c>x/y reviewed</c>
/// counter are computed from <see cref="IConfigReviewDiffService"/>'s diff dictionary,
/// so the filter MUST be computed from the same source. Reading per-ViewModel flags
/// (which are populated lazily as section ViewModels hydrate) produces the drift that
/// caused issue #665: counter said <c>5/7</c>, but 2 unreviewed rows were hidden and
/// unreachable, leaving the Apply button permanently disabled.
/// </para>
///
/// <para>
/// Any future change that routes the filter through per-VM flags (or any other state
/// store that can lag the service) will fail these tests, because none of them hand
/// the helper a SettingItemViewModel — only an id and the service.
/// </para>
/// </summary>
public class ReviewModeFilterTests
{
    // ---------- basic predicate behaviour ----------

    [Fact]
    public void ShouldShowInReviewQueue_WhenServiceHasDiff_ReturnsTrue()
    {
        var service = new Mock<IConfigReviewDiffService>();
        service.Setup(s => s.GetDiffForSetting("setting-a"))
               .Returns(new ConfigReviewDiff { SettingId = "setting-a" });

        ReviewModeFilter.ShouldShowInReviewQueue("setting-a", service.Object).Should().BeTrue();
    }

    [Fact]
    public void ShouldShowInReviewQueue_WhenServiceHasNoDiff_ReturnsFalse()
    {
        var service = new Mock<IConfigReviewDiffService>();
        service.Setup(s => s.GetDiffForSetting(It.IsAny<string>()))
               .Returns((ConfigReviewDiff?)null);

        ReviewModeFilter.ShouldShowInReviewQueue("setting-not-in-config", service.Object).Should().BeFalse();
    }

    // ---------- null safety ----------

    [Fact]
    public void ShouldShowInReviewQueue_WhenServiceIsNull_ReturnsFalse()
    {
        ReviewModeFilter.ShouldShowInReviewQueue("setting-a", null).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ShouldShowInReviewQueue_WhenSettingIdIsNullOrEmpty_ReturnsFalse(string? settingId)
    {
        var service = new Mock<IConfigReviewDiffService>();
        ReviewModeFilter.ShouldShowInReviewQueue(settingId, service.Object).Should().BeFalse();
        // Guard: the predicate must short-circuit before querying the service.
        service.Verify(s => s.GetDiffForSetting(It.IsAny<string>()), Times.Never);
    }

    // ---------- the issue #665 invariant ----------

    /// <summary>
    /// The bug-class invariant. Before #665 was fixed, the filter read per-VM flags
    /// (<c>HasReviewDiff</c> / <c>HasReviewAction</c>) populated lazily by
    /// <c>SettingReviewDiffApplier</c>. When a sub-page's ViewModels hadn't been hydrated
    /// at the time the user toggled "Only Changes", those flags were stuck at <c>false</c>
    /// while the service already knew about the diffs — so rows that still needed review
    /// were hidden and the Apply button could not be reached.
    ///
    /// This test enforces: for every setting id the service reports as part of the review
    /// queue, the filter shows the row. The helper is given only ids and the service,
    /// so there is no per-VM flag to consult even if a future regression tried to.
    /// </summary>
    [Fact]
    public void ShouldShowInReviewQueue_VisibilityFollowsServiceNotPerVmFlag()
    {
        var service = new Mock<IConfigReviewDiffService>();

        // The service knows about three diffs: an unreviewed value diff, an
        // already-reviewed (approved) diff, and an action-only diff. In the buggy
        // path, the action-only one and the freshly-hydrated unreviewed one would
        // have HasReviewDiff=false on their ViewModels and be hidden by the filter.
        service.Setup(s => s.GetDiffForSetting("unreviewed-value-diff"))
               .Returns(new ConfigReviewDiff { SettingId = "unreviewed-value-diff" });
        service.Setup(s => s.GetDiffForSetting("already-approved-diff"))
               .Returns(new ConfigReviewDiff
               {
                   SettingId = "already-approved-diff",
                   IsReviewed = true,
                   IsApproved = true
               });
        service.Setup(s => s.GetDiffForSetting("action-only-diff"))
               .Returns(new ConfigReviewDiff
               {
                   SettingId = "action-only-diff",
                   IsActionSetting = true,
                   ActionConfirmationMessage = "Apply taskbar clean?"
               });
        service.Setup(s => s.GetDiffForSetting("not-in-queue"))
               .Returns((ConfigReviewDiff?)null);

        ReviewModeFilter.ShouldShowInReviewQueue("unreviewed-value-diff", service.Object).Should().BeTrue(
            "an unreviewed diff that exists in the service must remain visible — this is the row the user was missing in #665");
        ReviewModeFilter.ShouldShowInReviewQueue("already-approved-diff", service.Object).Should().BeTrue(
            "an already-reviewed diff stays visible under the filter so the user can change their mind");
        ReviewModeFilter.ShouldShowInReviewQueue("action-only-diff", service.Object).Should().BeTrue(
            "an action-only diff is still part of the review queue and must remain visible");
        ReviewModeFilter.ShouldShowInReviewQueue("not-in-queue", service.Object).Should().BeFalse(
            "settings the service does not list as part of the review queue must be hidden by the filter");
    }
}
