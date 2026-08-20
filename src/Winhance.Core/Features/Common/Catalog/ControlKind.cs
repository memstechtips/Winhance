namespace Winhance.Core.Features.Common.Catalog;

// Presentation only: the engine resolves state from shape, never from this. Named ControlKind to avoid clashing
// with the WinUI Control type.
public enum ControlKind
{
    Toggle,
    Selection,
    Slider,
    Action,
    PowerPlan,
}
