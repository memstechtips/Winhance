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
}
