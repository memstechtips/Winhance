namespace Winhance.Core.Features.Common.Models;

public sealed record WinhanceTaskInfo(
    string Name,
    string Command,
    string Arguments,
    string RunsAs,
    bool Enabled,
    DateTime? LastRun = null,
    int? LastResult = null);
