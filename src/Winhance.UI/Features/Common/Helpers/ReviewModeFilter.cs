using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Helpers;

/// <summary>
/// Predicates for the "Only Changes" view filter shown during Config Review Mode.
/// </summary>
/// <remarks>
/// <para>
/// Visibility is driven by <see cref="IConfigReviewDiffService"/>'s diff dictionary,
/// which is the single source of truth for "is this setting in the review queue?".
/// The counter (<c>ReviewedChanges</c>/<c>TotalChanges</c>) and the Apply-button gate in
/// <see cref="Winhance.UI.ViewModels.ReviewModeBarViewModel"/> are also derived from
/// that same dictionary, so the filter, the counter, and the gate cannot drift apart.
/// </para>
/// <para>
/// Historically the filter checked per-ViewModel flags
/// (<c>SettingItemViewModel.HasReviewDiff</c> / <c>HasReviewAction</c>) which are populated
/// lazily by <c>SettingReviewDiffApplier</c> when each setting's ViewModel is hydrated.
/// That created a drift window: a setting whose diff was registered eagerly at
/// <c>EnterReviewModeAsync</c> but whose ViewModel had not yet been hydrated (sub-page
/// not yet visited, re-entry into review mode, etc.) had its flag stuck at <c>false</c>.
/// Toggling "Only Changes" then hid the row even though the service still counted it as
/// an unreviewed diff — leaving the user unable to reach <c>n/n</c> and the Apply button
/// disabled with no recourse. See GitHub issue #665 for the user-visible report and the
/// reproduction steps.
/// </para>
/// </remarks>
public static class ReviewModeFilter
{
    /// <summary>
    /// Returns true if the setting is in the review queue and should be shown when
    /// the "Only Changes" filter is active. Backed by the service, never by per-VM flags.
    /// </summary>
    /// <param name="settingId">The id of the setting being filtered.</param>
    /// <param name="diffService">The review diff service. If null (DI not yet wired, or
    /// the toggle is being applied outside review mode), the predicate returns false.</param>
    public static bool ShouldShowInReviewQueue(string? settingId, IConfigReviewDiffService? diffService)
    {
        if (diffService == null || string.IsNullOrEmpty(settingId)) return false;
        return diffService.GetDiffForSetting(settingId) != null;
    }
}
