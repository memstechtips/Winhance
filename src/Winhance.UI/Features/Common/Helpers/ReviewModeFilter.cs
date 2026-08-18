using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Helpers;

// Visibility is driven by IConfigReviewDiffService's diff dictionary - the single source of truth for "is this
// setting in the review queue?" - so the filter, the counter (ReviewedChanges/TotalChanges) and the Apply-button
// gate cannot drift apart. Per-ViewModel flags are populated lazily on hydration; filtering on those hid rows
// the service still counted as unreviewed, leaving n/n unreachable and Apply disabled (issue #665).
public static class ReviewModeFilter
{
    // Backed by the service, never by per-VM flags. A null service (DI not wired, or outside review mode) returns false.
    public static bool ShouldShowInReviewQueue(string? settingId, IConfigReviewDiffService? diffService)
    {
        if (diffService == null || string.IsNullOrEmpty(settingId)) return false;
        return diffService.GetDiffForSetting(settingId) != null;
    }
}
