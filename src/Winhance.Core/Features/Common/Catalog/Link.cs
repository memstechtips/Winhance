using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public enum LinkKind
{
    Requires,   // the owning setting needs OtherId in RequiredState (auto-applied if missing)
    Enables,    // applying the owning setting also forces OtherId to RequiredState
}

public sealed record Link(string OtherId, LinkKind Kind, LocKey RequiredState)
{
    // Default true; auto-enable links set this false (force the other into its state, but no reverse).
    public bool ReverseCascade { get; init; } = true;

    public bool Force { get; init; }
}
