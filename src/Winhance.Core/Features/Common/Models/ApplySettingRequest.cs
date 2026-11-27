namespace Winhance.Core.Features.Common.Models;

public sealed record ApplySettingRequest
{
    public required string SettingId { get; init; }
    public required bool Enable { get; init; }
    public object? Value { get; init; }
    public bool CheckboxResult { get; init; }
    public bool ApplyRecommended { get; init; }
    public bool SkipValuePrerequisites { get; init; }
    // Applies the WindowsDefault state with its per-target ResetSet overrides (the [1,null] Explorer settings DELETE
    // instead of writing Set). Also set by the reverse cascade (ApplyAction.IsReset).
    public bool ResetToDefault { get; init; }

    // The change-history receipt's "before" half, when the caller already knows it. A card has just displayed
    // this setting's state, so re-reading the setting we are about to change buys nothing. Null from every
    // caller that has no card behind it (config import, bulk actions, the relationship cascade), and those
    // still read.
    public SettingStateResult? BeforeState { get; init; }
}
