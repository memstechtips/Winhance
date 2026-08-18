namespace Winhance.Core.Features.Common.Models;

public sealed record ApplySettingRequest
{
    public required string SettingId { get; init; }
    public required bool Enable { get; init; }
    public object? Value { get; init; }
    public bool CheckboxResult { get; init; }
    public bool ApplyRecommended { get; init; }
    public bool SkipValuePrerequisites { get; init; }
    // On the catalog engine this applies the WindowsDefault state with its per-target ResetSet overrides (the
    // [1,null] Explorer settings DELETE instead of writing Set). On the fallback path it writes DisabledValue[1] (the
    // parent-cascade value) instead of DisabledValue[0]. Also set by the reverse cascade (ApplyAction.IsReset).
    public bool ResetToDefault { get; init; }
}
