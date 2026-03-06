namespace Winhance.Core.Features.Common.Models;

public record ScheduledTaskSetting
{
    public string Id { get; init; } = string.Empty;
    public string TaskPath { get; init; } = string.Empty;
    public bool? RecommendedState { get; init; }
}
