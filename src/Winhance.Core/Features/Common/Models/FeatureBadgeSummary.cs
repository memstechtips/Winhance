namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Aggregated counts for a feature shown on its overview card.
///
/// Two independent axes, deliberately kept apart:
/// <list type="bullet">
/// <item><see cref="RecommendedCount"/> / <see cref="DefaultCount"/> - WHERE the settings sit relative to
/// our advice. Always shown, as "N/total".</item>
/// <item><see cref="UnrecognizedCount"/> / <see cref="MalformedCount"/> / <see cref="UndeterminedCount"/> -
/// settings detection could not place, split by WHY. Shown only when non-zero, each with the same icon the
/// setting's own control carries, so the overview says at a glance which kind of problem is inside instead
/// of just "some are Custom".</item>
/// </list>
/// </summary>
public sealed record FeatureBadgeSummary(
    int TotalWithBadgeData,
    int RecommendedCount,
    int DefaultCount,
    int NewCount,
    int UnrecognizedCount,
    int MalformedCount,
    int UndeterminedCount)
{
    /// <summary>Settings detection could not place, whatever the reason.</summary>
    public int UnresolvedCount => UnrecognizedCount + MalformedCount + UndeterminedCount;
}
