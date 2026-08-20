namespace Winhance.Core.Features.Common.Catalog;

public sealed record Setting
{
    public required string Id { get; init; }                         // the contract: configs + loc keys key off this
    public required Display Display { get; init; }

    public IReadOnlyList<PowerContext> Contexts { get; init; } = new[] { PowerContext.Always };
    public IReadOnlyList<Target> Targets { get; init; } = System.Array.Empty<Target>();
    public IReadOnlyList<SettingState> States { get; init; } = System.Array.Empty<SettingState>();

    public Numeric? Numeric { get; init; }

    // The Action mechanism: a stateless one-shot whose Effects run on click. Empty for every detected setting -
    // toggles and selections carry their effects per state.
    public IReadOnlyList<Effect> Effects { get; init; } = System.Array.Empty<Effect>();

    // UN-BAKED setting-level scripts (placeholders like {{primary}} intact) that the autounattend Custom state runs
    // with the config item's CustomStateValues substituted - a Selection value matching no preset option, so no
    // state's baked ScriptEffects apply.
    public IReadOnlyList<ScriptEffect> CustomStateScripts { get; init; } = System.Array.Empty<ScriptEffect>();

    public IStateDetector? Detector { get; init; }

    public IDynamicOptionSource? OptionSource { get; init; }

    public Availability Availability { get; init; } = Availability.Everywhere;
    public ApplyBehavior Apply { get; init; } = ApplyBehavior.None;

    // Forward relationships (Requires/Enables) live on SettingState.Links - they are a property of the
    // state that triggers them, like Controls. ResolveReverseCascade/CatalogValidator read States.SelectMany(Links).

    // Presentation only: nesting says where the card is drawn, NOT that it stops meaning anything - a setting that
    // is inert in some of the parent's states says so itself, in EnabledWhen.
    public string? UiParentId { get; init; }

    // Independent of UiParentId: a gate may name a setting this one is not nested under.
    public EnabledWhen? EnabledWhen { get; init; }

    // DERIVED from the setting shape so it can never drift from what the engine detects. Toggle == exactly two
    // states labelled Enabled/Disabled - the invariant live detection relies on.
    public ControlKind Control =>
        OptionSource is not null ? ControlKind.PowerPlan
        : Numeric is not null ? ControlKind.Slider
        : States.Count == 0 ? ControlKind.Action
        : States.Count == 2 && States.All(s => s.Label is "Enabled" or "Disabled") ? ControlKind.Toggle
        : ControlKind.Selection;
}
