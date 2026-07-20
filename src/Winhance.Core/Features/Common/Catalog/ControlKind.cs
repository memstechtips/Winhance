namespace Winhance.Core.Features.Common.Catalog;

/// <summary>How a setting is RENDERED (a presentation concern, not an engine one). The engine resolves
/// state from shape (Numeric/States/
/// OptionSource), never from this. No CheckBox - no setting uses it (only the apps-side ItemDefinition did).
/// Named ControlKind (not Control) to avoid clashing with the WinUI Control type in the UI project.</summary>
public enum ControlKind
{
    Toggle,
    Selection,
    Slider,
    Action,
    PowerPlan,
}
