using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

// One removal script / scheduled task pair as Task Scheduler and the disk really have it.
public sealed record RemovalTaskState(
    string Name,
    RemovalTaskHealth Health,
    string ScriptPath,
    DateTime? LastRun,
    int? LastResult,
    IReadOnlyList<string> Items);
