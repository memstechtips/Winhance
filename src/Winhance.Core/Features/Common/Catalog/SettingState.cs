using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public sealed record SettingState
{
    // The state's identity inside its setting as well as a Selection's caption. A Toggle or CheckBox never renders it.
    public required LocKey Label { get; init; }

    public LocKey? Tooltip { get; init; }
    // Shown when this state is chosen: an outcome note that only makes sense once it is.
    public LocKey? Warning { get; init; }

    // Shown on the card whatever is selected, because the harm lands the moment this state is chosen. The text
    // names the state it is about.
    public LocKey? RiskWarning { get; init; }
    public IReadOnlyList<StateRole> Roles { get; init; } = System.Array.Empty<StateRole>();
    public IReadOnlyDictionary<string, StateValue> Set { get; init; } =
        new Dictionary<string, StateValue>();

    // Apply-only override of Set for a reset-to-default (per-card / bulk Reset and the reverse cascade): keys listed
    // here write THIS value on reset (e.g. Absent to delete), the rest fall back to Set. Lets a state DETECT
    // "1-or-absent" yet DELETE on reset. Only the WindowsDefault state needs one.
    public IReadOnlyDictionary<string, StateValue>? ResetSet { get; init; }

    public IReadOnlyList<Effect> Effects { get; init; } = System.Array.Empty<Effect>();

    // The engine resolves here when NO other state's Set matches, instead of returning Custom - for settings whose
    // default is "any unrecognised value" (e.g. a REG_BINARY blob that varies between installs). One per setting
    // (validator-enforced); a fallback with a Set still resolves normally when that Set matches.
    public bool IsFallback { get; init; }

    // A state detection can RESOLVE TO but the user cannot CHOOSE: left out of the option list. Independent of
    // IsFallback; together they are the shape a NEUTRAL state wants (a configuration Winhance can read but not write,
    // e.g. the two theme sub-toggles disagreeing). May not carry a role, be a Controls value or a Link.RequiredState
    // (validator-enforced). SKIP, NEVER RENUMBER: option index == state index is persisted by saved configs, the
    // autounattend generator and the review diff.
    public bool IsDetectOnly { get; init; }

    public IReadOnlyDictionary<string, LocKey>? Controls { get; init; }

    // Fired by APPLYING this state; a deactivation state simply declares none, so a default-ON owner's WindowsDefault
    // state can still fire its prerequisites.
    public IReadOnlyList<Link> Links { get; init; } = System.Array.Empty<Link>();

    // UNCONDITIONAL roles only: a build-scoped role is deliberately not matched, so build-unaware readers (badges,
    // the validator's one-per-context rule, relationship resolution) see a merged setting's OS-divergent default as
    // "no single default". Use the WinBuild overload to resolve the default for a build.
    public bool HasRole(RoleKind kind, PowerContext context = PowerContext.Always) =>
        Roles.Any(r => r.Kind == kind && r.Context == context && r.AppliesTo.Count == 0);

    // The reset resolver uses this so an OS-divergent Windows default resolves to the state that is default on the LIVE build.
    public bool HasRole(RoleKind kind, WinBuild build, PowerContext context = PowerContext.Always) =>
        Roles.Any(r => r.Kind == kind && r.Context == context
            && (r.AppliesTo.Count == 0 || r.AppliesTo.Any(range => range.Contains(build))));
}
