namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Typed metadata for power setting recommendations and dynamic option loading.
/// </summary>
public sealed record PowerRecommendation
{
    public string? RecommendedOptionAC { get; init; }
    public string? RecommendedOptionDC { get; init; }
    public bool LoadDynamicOptions { get; init; }
}
