using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A setting: identity (Id) + what the user sees (Display) + where/how (Targets) + what (States) +
/// optional custom Detector, gating (Availability), apply behaviour (Apply), and relationships. The new
/// unified model. Pure data; not yet wired into live services.</summary>
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
    /// the catalog home of what the old emitter read off SettingDefinition.PowerShellScripts for a Selection with
    /// no SelectedIndex (a "Custom" value matching no preset option, so no state's baked ScriptEffects apply).
    /// Converter-sourced from ALL of def.PowerShellScripts, each as (EnabledScript raw + RunContext); only the
    /// script-bearing Selections carry any. (The old PowerShellScriptSetting.RequiresElevation flag is never read
    /// by the emit, so it is deliberately not modelled.)</summary>
    public IReadOnlyList<ScriptEffect> CustomStateScripts { get; init; } = System.Array.Empty<ScriptEffect>();

    public IStateDetector? Detector { get; init; }

    /// <summary>Set when this setting's options are produced at runtime (e.g. the installed power plans) rather
    /// than authored as static States. Null = static options/states.</summary>
    public IDynamicOptionSource? OptionSource { get; init; }

    public Availability Availability { get; init; } = Availability.Everywhere;   // gating
    public ApplyBehavior Apply { get; init; } = ApplyBehavior.None;              // confirmation + restart

    // Forward relationships (Requires/Enables) moved onto SettingState.Links (Phase 6.6) - they are a property of the
    // state that triggers them, like Controls. ResolveReverseCascade/CatalogValidator now read States.SelectMany(Links).

    /// <summary>Presentation only: nest this setting under the parent in the UI and disable its control when
    /// the parent is off. No apply behaviour. Null = top-level.</summary>
    public string? UiParentId { get; init; }

    /// <summary>The render-kind, DERIVED from the setting shape - the single source of truth, so it can never
    /// drift from what the engine detects. Presentation reads this to pick a control; the engine resolves state
    /// from the same shape directly (so "explicit render-kind" and "shape-driven engine" are one thing). Toggle ==
    /// exactly two states both labelled "Enabled"/"Disabled" (the converter invariant the live detection already
    /// relies on); everything else follows from OptionSource / Numeric / States presence.</summary>
    public ControlKind Control =>
        OptionSource is not null ? ControlKind.PowerPlan
        : Numeric is not null ? ControlKind.Slider
        : States.Count == 0 ? ControlKind.Action
        : States.Count == 2 && States.All(s => s.Label is "Enabled" or "Disabled") ? ControlKind.Toggle
        : ControlKind.Selection;
}
