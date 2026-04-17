namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Aggregated badge counts for a feature shown on overview cards.
/// </summary>
public sealed record FeatureBadgeSummary(
    int TotalWithBadgeData,
    int RecommendedCount,
    int DefaultCount,
    int CustomCount,
    int NewCount);
