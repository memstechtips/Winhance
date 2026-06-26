using System.Collections.Generic;

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
}
