using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>One choice the user can pick. A toggle has two; an Action has none. Detection reads <see cref="Set"/>;
/// <see cref="Effects"/> ride along on apply only.</summary>
public sealed record SettingState
{
    public required string Label { get; init; }                          // localization key
    public string? Tooltip { get; init; }                                // per-option tooltip (localized at read time via OptionTooltip key); null = none
    public string? Warning { get; init; }                                // per-option warning (localized at read time via OptionWarning key); null = none
    public IReadOnlyList<StateRole> Roles { get; init; } = System.Array.Empty<StateRole>();
    public IReadOnlyDictionary<string, StateValue> Set { get; init; } =
        new Dictionary<string, StateValue>();

    /// <summary>Apply-only override of <see cref="Set"/> for a RESET-to-default apply (the per-card / bulk
    /// "Reset to Defaults" and the relationship reverse-cascade). Null = a reset writes exactly what
    /// <see cref="Set"/> writes. Present = for each target key listed, the reset write is THIS
    /// <see cref="StateValue"/> (e.g. <see cref="StateValue.Absent"/> to DELETE) instead of the detect/normal-apply
    /// <see cref="Set"/> value; target keys NOT in ResetSet fall back to <see cref="Set"/>. This lets a state DETECT
    /// "1-or-absent" yet on reset DELETE - the divergence the old apply expressed via <c>DisabledValue[1] == null</c>
    /// (GetParentDisableValue). Only the WindowsDefault state needs one, and only for the targets whose reset write
    /// differs from their normal Set write.</summary>
    public IReadOnlyDictionary<string, StateValue>? ResetSet { get; init; }

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

    /// <summary>True when this state carries an UNCONDITIONAL role of the given kind/context (a role with no
    /// build scope). A build-scoped role (non-empty <see cref="StateRole.AppliesTo"/>) is deliberately NOT matched
    /// here - it is not an unconditional role - so build-unaware readers (UI badges, the validator's one-per-context
    /// rule, relationship resolution) see a merged setting's OS-divergent default as "no single default", exactly as
    /// they did before build-scoped roles existed. Use the build-aware overload to resolve the default for a build.</summary>
    public bool HasRole(RoleKind kind, PowerContext context = PowerContext.Always) =>
        Roles.Any(r => r.Kind == kind && r.Context == context && r.AppliesTo.Count == 0);

    /// <summary>Build-aware role query: matches an unconditional role (empty <see cref="StateRole.AppliesTo"/>) OR
    /// one whose AppliesTo admits <paramref name="build"/>. The reset resolver uses this so a merged setting's
    /// OS-divergent Windows default resolves to the state that is default on the LIVE build.</summary>
    public bool HasRole(RoleKind kind, WinBuild build, PowerContext context = PowerContext.Always) =>
        Roles.Any(r => r.Kind == kind && r.Context == context
            && (r.AppliesTo.Count == 0 || r.AppliesTo.Any(range => range.Contains(build))));
}
