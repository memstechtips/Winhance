namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Typed metadata for numeric range settings (slider/spinner controls).
/// </summary>
public record NumericRangeMetadata
{
    public required int MinValue { get; init; }
    public required int MaxValue { get; init; }
    public int Increment { get; init; } = 1;
    public string? Units { get; init; }
}
