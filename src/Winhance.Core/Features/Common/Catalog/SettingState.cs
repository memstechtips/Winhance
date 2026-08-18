namespace Winhance.Core.Features.Common.Catalog;

public sealed record SettingState
{
    public required string Label { get; init; }                          // localization key
    public string? Tooltip { get; init; }                                // per-option tooltip (localized at read time via OptionTooltip key); null = none
    public string? Warning { get; init; }                                // per-option warning (localized at read time via OptionWarning key); null = none
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

    public IReadOnlyDictionary<string, string>? Controls { get; init; }

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
