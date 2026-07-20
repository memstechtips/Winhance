namespace Winhance.Core.Features.Common.Models;

public sealed record ApplySettingRequest
{
    public required string SettingId { get; init; }
    public required bool Enable { get; init; }
    public object? Value { get; init; }
    public bool CheckboxResult { get; init; }
    public bool ApplyRecommended { get; init; }
    public bool SkipValuePrerequisites { get; init; }
    /// <summary>
    /// When true, applies the setting's default state as a RESET rather than a normal apply. On the catalog
    /// engine (ApplyRequestResolver) this applies the WindowsDefault-roled state with its per-target ResetSet
    /// overrides - the [1,null] Explorer settings DELETE instead of writing their normal Set value.
    /// On the fallback path (unpaired / custom-detector settings) it writes DisabledValue[1] (the parent-cascade
    /// value; e.g. null to delete the value for a clean slate) instead of DisabledValue[0], falling back to normal
    /// disable when no second element exists. Also set by the relationship reverse-cascade (ApplyAction.IsReset).
    /// </summary>
    public bool ResetToDefault { get; init; }
}
