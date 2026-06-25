namespace Winhance.Core.Features.Common.Catalog;

/// <summary>How one setting relates to another. Merges the old dependencies + auto-enable into one
/// directional relationship; the required target state is always named explicitly.</summary>
public enum LinkKind
{
    Requires,   // the owning setting needs OtherId in RequiredState (auto-applied if missing)
    Enables,    // applying the owning setting also forces OtherId to RequiredState
}

/// <summary>A directional relationship from the owning setting to <see cref="OtherId"/>, naming the exact
/// state OtherId must be in.</summary>
public sealed record Link(string OtherId, LinkKind Kind, string RequiredState)
{
    /// <summary>When the requirement is later broken, cascade-reset the owning setting. Default true; the
    /// old auto-enable behaviour sets this false (forcing the other into its state, but no reverse).</summary>
    public bool ReverseCascade { get; init; } = true;

    /// <summary>Re-apply the target even if it is already in the wanted state (old auto-enable forced an event).</summary>
    public bool Force { get; init; }
}
