namespace Winhance.Core.Features.Common.Models;

// Two independent axes, deliberately kept apart: Recommended/Default counts say WHERE the settings sit relative
// to our advice (always shown as N/total); Unrecognized/Malformed/Undetermined say WHY detection could not place
// a setting (shown only when non-zero, with the control's own icon).
public sealed record FeatureBadgeSummary(
    int TotalWithBadgeData,
    int RecommendedCount,
    int DefaultCount,
    int NewCount,
    int UnrecognizedCount,
    int MalformedCount,
    int UndeterminedCount)
{
    public int UnresolvedCount => UnrecognizedCount + MalformedCount + UndeterminedCount;
}
