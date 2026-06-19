namespace Winhance.Core.Features.Common.Catalog;

/// <summary>How one setting relates to another. Merges the old separate "dependencies" and
/// "auto-enable" lists into one directional relationship, the differences captured as flags.</summary>
public enum LinkKind
{
    RequiresEnabled,   // the owning setting needs OtherId enabled
    RequiresDisabled,  // the owning setting needs OtherId disabled
    RequiresValue,     // the owning setting needs OtherId at RequiredValue
    Enables,           // enabling the owning setting also enables OtherId
}

/// <summary>A directional relationship from the owning setting to <see cref="OtherId"/>.</summary>
public sealed record Link(string OtherId, LinkKind Kind)
{
    /// <summary>For <see cref="LinkKind.RequiresValue"/>: the state label OtherId must be in.</summary>
    public string? RequiredValue { get; init; }

    /// <summary>When the requirement is later broken, cascade-reset the owning setting. Default true; the
    /// old auto-enable behaviour sets this false (enabling forces the other on, but no reverse).</summary>
    public bool ReverseCascade { get; init; } = true;

    /// <summary>Re-apply the target even if it is already in the wanted state (old auto-enable forced an event).</summary>
    public bool Force { get; init; }
}
