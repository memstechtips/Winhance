using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

// One removal script / scheduled task pair as Task Scheduler and the disk really have it.
public sealed record RemovalTaskState(
    string Name,
    bool? TaskEnabled,
    bool ScriptExists,
    string ScriptPath,
    DateTime? LastRun,
    int? LastResult,
    IReadOnlyList<string> Items)
{
    public bool TaskExists => TaskEnabled.HasValue;

    public RemovalTaskHealth Health => !TaskExists ? RemovalTaskHealth.NoTask
        : !ScriptExists ? RemovalTaskHealth.ScriptMissing
        : TaskEnabled == false ? RemovalTaskHealth.TaskDisabled
        : RemovalTaskHealth.Active;
}
