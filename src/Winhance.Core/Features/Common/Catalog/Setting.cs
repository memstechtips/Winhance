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

    public CheckList? CheckList { get; init; }

    // A Selection's presets spelled out as typed fields, one per target key, in rows (one DNS server per row): picking
    // a preset fills them, and typing a value no preset has is the Custom state. Edited behind the card's pencil and
    // applied together.
    public IReadOnlyList<IReadOnlyList<Field>> Fields { get; init; } = System.Array.Empty<IReadOnlyList<Field>>();

    // The Action mechanism: a stateless one-shot whose Effects run on click. Empty for every detected setting -
    // toggles and selections carry their effects per state.
    public IReadOnlyList<Effect> Effects { get; init; } = System.Array.Empty<Effect>();

    // Scripts for what no state carries. The answer file fills their placeholders ({{value}}, {{servers}}) from the
    // choice and runs them on the new install; a script-only Selection also runs them live for a Custom value.
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
    // ScriptEffect, or a text box's or option list's CustomStateScripts, is run by Winhancements.ps1 on the new install. An option
    // list normally means a reading off this machine, but a card whose every target is an answer-file element is
    // offering choices for media this PC has never seen, so there is nothing here to detect or apply.
    public bool IsAnswerFileOnly =>
        Detector is null && Numeric is null
        && (Options is null || Targets.All(t => t is AutounattendTarget))
        && (Targets.Any(t => t is AutounattendTarget) || Effects.Count > 0 || States.Any(s => s.Effects.Count > 0)
            || ((TextBox is not null || Options is not null) && CustomStateScripts.Count > 0))
        && Targets.All(t => t is AutounattendTarget || t is RegTarget { ReadOnly: true })
        && Effects.All(e => e is AutounattendCommand or AutounattendFirstLogonCommand)
        && States.SelectMany(s => s.Effects).All(e => e is AutounattendCommand or AutounattendFirstLogonCommand or ScriptEffect);

    // Where an answer-file card starts, and what the file renders for a setting it holds no choice for.
    public SettingState? SeedState =>
        States.FirstOrDefault(s => s.HasRole(RoleKind.Recommended)) ?? States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault));

    // Such a card opens on this PC's reading, so nothing that sets cards by role may overwrite it.
    public bool SeedsFromThisPc => IsAnswerFileOnly && Targets.OfType<RegTarget>().Any(t => t.ReadOnly);

    // Derived from the setting shape, never authored, so it cannot drift from what the engine detects.
    public ControlKind Control =>
        CheckList is not null ? ControlKind.CheckList
        : Options is not null ? ControlKind.KeyedSelection
        : Numeric is not null ? ControlKind.Slider
        : TextBox is not null ? ControlKind.TextBox
        : List is not null ? ControlKind.List
        : States.Count == 0 ? ControlKind.Action
        : TwoState.Matches(States, ControlKind.Toggle) ? ControlKind.Toggle
        : TwoState.Matches(States, ControlKind.CheckBox) ? ControlKind.CheckBox
        : ControlKind.Selection;
}
