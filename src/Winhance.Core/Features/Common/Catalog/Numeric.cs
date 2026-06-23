using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A numeric (slider) setting: a continuous range instead of enumerated states. Min/Max bound the
/// value and Units labels it; Recommended/WindowsDefault carry the per-context target values (AC and DC for a
/// battery-aware power setting, or a single Always entry for a context-free numeric). Detection reads the raw
/// value rather than matching a state. Null on a Setting means the setting is state-based, not a slider.</summary>
public sealed record Numeric
{
    public required int Min { get; init; }
    public required int Max { get; init; }
    public string? Units { get; init; }
    public IReadOnlyList<ContextValue> Recommended { get; init; } = Array.Empty<ContextValue>();
    public IReadOnlyList<ContextValue> WindowsDefault { get; init; } = Array.Empty<ContextValue>();
}

/// <summary>An int value scoped to a power context (Always for a context-free numeric).</summary>
public sealed record ContextValue(PowerContext Context, int Value);
