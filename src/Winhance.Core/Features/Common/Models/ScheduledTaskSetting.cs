namespace Winhance.Core.Features.Common.Models;

public record ScheduledTaskSetting
{
    public string Id { get; init; } = string.Empty;
    public string TaskPath { get; init; } = string.Empty;
    public required bool? RecommendedState { get; init; }
    public required bool? DefaultState { get; init; }
}
