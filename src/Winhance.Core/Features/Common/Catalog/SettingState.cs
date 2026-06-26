using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>One choice the user can pick. A toggle has two; an Action has none. Detection reads <see cref="Set"/>;
/// <see cref="Effects"/> ride along on apply only.</summary>
public sealed record SettingState
{
    public required string Label { get; init; }                          // localization key
    public IReadOnlyList<StateRole> Roles { get; init; } = System.Array.Empty<StateRole>();
    public IReadOnlyDictionary<string, StateValue> Set { get; init; } =
        new Dictionary<string, StateValue>();
    public IReadOnlyList<Effect> Effects { get; init; } = System.Array.Empty<Effect>();

    /// <summary>
    /// Catch-all: the engine resolves here when NO other state's Set matches the live readings, instead of
    /// returning Custom. Replaces the old <c>ResolveUnmatchedToDefault</c>. Needed for settings whose default
    /// is "any unrecognised value" rather than an enumerable value or absence (e.g. a REG_BINARY blob whose
    /// default content varies between installs). Exactly one state per setting may set this (validator-enforced).
    /// A fallback state may still carry a <c>Set</c> (its representative known value); if that Set
    /// matches it resolves normally, otherwise it is the catch-all.
    /// </summary>
    public bool IsFallback { get; init; }

    /// <summary>When this selection state is active, the child settings it drives and the state each must
    /// be in (childId → required state label). Replaces the old preset map. Null = controls nothing.</summary>
    public IReadOnlyDictionary<string, string>? Controls { get; init; }

    /// <summary>Forward relationships triggered by APPLYING this state: the prerequisites it Requires and the
    /// settings it Enables (the per-state home of what used to be the setting-level Links). Applying this state
    /// fires them; a deactivation/off state simply declares none - so there is no role-based skip, and a default-ON
    /// owner's active (WindowsDefault) state can still fire its prerequisites. Empty = none.</summary>
    public IReadOnlyList<Link> Links { get; init; } = System.Array.Empty<Link>();

    public bool HasRole(RoleKind kind, PowerContext context = PowerContext.Always) =>
        Roles.Any(r => r.Kind == kind && r.Context == context);
}
