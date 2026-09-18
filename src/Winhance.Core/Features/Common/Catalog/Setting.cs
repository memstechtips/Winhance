using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Catalog;

public sealed record Setting
{
    public required string Id { get; init; }                         // the contract: configs + loc keys key off this
    public required Display Display { get; init; }

    public IReadOnlyList<PowerContext> Contexts { get; init; } = new[] { PowerContext.Always };
    public IReadOnlyList<Target> Targets { get; init; } = System.Array.Empty<Target>();
    public IReadOnlyList<SettingState> States { get; init; } = System.Array.Empty<SettingState>();

    public Numeric? Numeric { get; init; }

    public TextBox? TextBox { get; init; }

    public List? List { get; init; }

    // The Action mechanism: a stateless one-shot whose Effects run on click. Empty for every detected setting -
    // toggles and selections carry their effects per state.
    public IReadOnlyList<Effect> Effects { get; init; } = System.Array.Empty<Effect>();

    // UN-BAKED setting-level scripts (placeholders like {{primary}} intact) that the autounattend Custom state runs
    // with the config item's CustomStateValues substituted - a Selection value matching no preset option, so no
    // state's baked ScriptEffects apply.
    public IReadOnlyList<ScriptEffect> CustomStateScripts { get; init; } = System.Array.Empty<ScriptEffect>();

    public IStateDetector? Detector { get; init; }

    public OptionList? Options { get; init; }

    public Availability Availability { get; init; } = Availability.Everywhere;
    public ApplyBehavior Apply { get; init; } = ApplyBehavior.None;

    // Presentation only: nesting says where the card is drawn, NOT that it stops meaning anything - a setting that
    // is inert in some of the parent's states says so itself, in EnabledWhen.
    public string? UiParentId { get; init; }

    // Independent of UiParentId: it may name a setting this one is not nested under.
    public StateGate? EnabledWhen { get; init; }

    public StateGate? VisibleWhen { get; init; }

    // Never applied to a machine. A ReadOnly registry target only seeds the card from this PC, and a state's
    // ScriptEffect is run by Winhancements.ps1 on the new install.
    public bool IsAnswerFileOnly =>
        Detector is null && Options is null && Numeric is null
        && (Targets.Any(t => t is AutounattendTarget) || Effects.Count > 0 || States.Any(s => s.Effects.Count > 0))
        && Targets.All(t => t is AutounattendTarget || t is RegTarget { ReadOnly: true })
        && Effects.All(e => e is AutounattendCommand or AutounattendFirstLogonCommand)
        && States.SelectMany(s => s.Effects).All(e => e is AutounattendCommand or AutounattendFirstLogonCommand or ScriptEffect);

    // Derived from the setting shape, never authored, so it cannot drift from what the engine detects.
    public ControlKind Control =>
        Options is not null ? ControlKind.KeyedSelection
        : Numeric is not null ? ControlKind.Slider
        : TextBox is not null ? ControlKind.TextBox
        : List is not null ? ControlKind.List
        : States.Count == 0 ? ControlKind.Action
        : TwoState.Matches(States, ControlKind.Toggle) ? ControlKind.Toggle
        : TwoState.Matches(States, ControlKind.CheckBox) ? ControlKind.CheckBox
        : ControlKind.Selection;
}
