namespace Winhance.Core.Features.SoftwareApps.Models;

public class RemovalScript
{
    public string Name { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string TargetScheduledTaskName { get; init; } = string.Empty;
    public bool RunOnStartup { get; init; }
    public string? ActualScriptPath { get; init; }
}
