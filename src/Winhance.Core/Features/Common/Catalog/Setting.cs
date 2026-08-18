namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A setting: identity (Id) + what the user sees (Display) + where/how (Targets) + what (States) +
/// optional custom Detector, gating (Availability), apply behaviour (Apply), and relationships. Pure data.</summary>
public sealed record Setting
{
    public required string Id { get; init; }                         // the contract: configs + loc keys key off this
    public required Display Display { get; init; }                   // everything the user sees

    public IReadOnlyList<PowerContext> Contexts { get; init; } = new[] { PowerContext.Always };
    public IReadOnlyList<Target> Targets { get; init; } = System.Array.Empty<Target>();
    public IReadOnlyList<SettingState> States { get; init; } = System.Array.Empty<SettingState>();

    /// <summary>A slider setting's range + per-context recommended/default values; null for a state-based
    /// setting. A Numeric setting carries this instead of enumerated States.</summary>
    public Numeric? Numeric { get; init; }

    /// <summary>Apply-only side-effects that run when this setting is applied, independent of any state. The
    /// Action mechanism: a stateless one-shot (no States/Targets) whose Effects run on click. Empty for every
    /// detected setting - toggles/selections carry their effects per-state on SettingState.Effects.</summary>
    public IReadOnlyList<Effect> Effects { get; init; } = System.Array.Empty<Effect>();

    /// <summary>The UN-BAKED setting-level scripts (placeholders like <c>{{primary}}</c> intact, source order)
    /// that the autounattend script-gen CUSTOM state runs with the config item's CustomStateValues substituted -
    /// for a Selection with no SelectedIndex (a "Custom" value matching no preset option, so no state's baked
    /// ScriptEffects apply). Only the script-bearing Selections carry any.</summary>
    public IReadOnlyList<ScriptEffect> CustomStateScripts { get; init; } = System.Array.Empty<ScriptEffect>();

    public IStateDetector? Detector { get; init; }

    /// <summary>Set when this setting's options are produced at runtime (e.g. the installed power plans) rather
    /// than authored as static States. Null = static options/states.</summary>
    public IDynamicOptionSource? OptionSource { get; init; }

    public Availability Availability { get; init; } = Availability.Everywhere;   // gating
    public ApplyBehavior Apply { get; init; } = ApplyBehavior.None;              // confirmation + restart

    // Forward relationships (Requires/Enables) live on SettingState.Links - they are a property of the
    // state that triggers them, like Controls. ResolveReverseCascade/CatalogValidator read States.SelectMany(Links).

    /// <summary>Presentation only: nest this setting under the parent in the UI. No apply behaviour, and
    /// NO gating - nesting says where the card is drawn, not that it stops meaning anything. A setting that
    /// really is inert in some of the parent's states says so itself, in <see cref="EnabledWhen"/>.
    /// Null = top-level.</summary>
    public string? UiParentId { get; init; }

    /// <summary>The declared presentation gate: the setting whose current state decides whether THIS
    /// setting's control is usable, and the state labels in which it is. Null (the default, and the case
    /// for most nested settings) = never gated. Independent of <see cref="UiParentId"/>: a gate may name a
    /// setting this one is not nested under, and most nested settings declare no gate at all.</summary>
    public EnabledWhen? EnabledWhen { get; init; }

    /// <summary>The render-kind, DERIVED from the setting shape - the single source of truth, so it can never
    /// drift from what the engine detects. Presentation reads this to pick a control; the engine resolves state
    /// from the same shape directly (so "explicit render-kind" and "shape-driven engine" are one thing). Toggle ==
    /// exactly two states both labelled "Enabled"/"Disabled" (the invariant the live detection relies on);
    /// everything else follows from OptionSource / Numeric / States presence.</summary>
    public ControlKind Control =>
        OptionSource is not null ? ControlKind.PowerPlan
        : Numeric is not null ? ControlKind.Slider
        : States.Count == 0 ? ControlKind.Action
        : States.Count == 2 && States.All(s => s.Label is "Enabled" or "Disabled") ? ControlKind.Toggle
        : ControlKind.Selection;
}
