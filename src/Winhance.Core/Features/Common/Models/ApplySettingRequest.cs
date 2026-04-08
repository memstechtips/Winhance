namespace Winhance.Core.Features.Common.Models;

public record ApplySettingRequest
{
    public required string SettingId { get; init; }
    public required bool Enable { get; init; }
    public object? Value { get; init; }
    public bool CheckboxResult { get; init; }
    public string? CommandString { get; init; }
    public bool ApplyRecommended { get; init; }
    public bool SkipValuePrerequisites { get; init; }
    /// <summary>
    /// When true, uses DisabledValue[1] (parent cascade value) instead of DisabledValue[0]
    /// for each registry setting. Settings can declare e.g. DisabledValue = [1, null] where
    /// index 0 is the explicit disable value and index 1 is the value to write when the
    /// parent cascades a disable (e.g. null to delete the value for a clean slate).
    /// If no second element exists, falls back to normal disable behavior.
    /// </summary>
    public bool ResetToDefault { get; init; }
}
