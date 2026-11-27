namespace Winhance.Core.Features.Common.Catalog;

public sealed record Numeric
{
    public required int Min { get; init; }
    public required int Max { get; init; }
    public string? Units { get; init; }
    public IReadOnlyList<ContextValue> Recommended { get; init; } = Array.Empty<ContextValue>();
    public IReadOnlyList<ContextValue> WindowsDefault { get; init; } = Array.Empty<ContextValue>();
}

public sealed record ContextValue(PowerContext Context, int Value);
