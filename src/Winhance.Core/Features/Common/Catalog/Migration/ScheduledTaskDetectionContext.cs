using System;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: the minimal <see cref="IDetectionContext"/> the scheduled-task
/// equivalence harness needs. It carries one pre-fetched task-enabled value (the harness reads it async up
/// front, since the context API is synchronous); the registry and network members are never reached by a pure
/// scheduled-task toggle and throw if called. Deleted once the migration is complete.</summary>
public sealed class ScheduledTaskDetectionContext : IDetectionContext
{
    private readonly bool? _enabled;

    public ScheduledTaskDetectionContext(bool? enabled) => _enabled = enabled;

    public WinBuild CurrentBuild => new(int.MaxValue); // scheduled-task settings are not build-gated

    public bool? ScheduledTaskEnabled(string taskPath) => _enabled;

    public object? GetValue(string keyPath, string? valueName) =>
        throw new NotSupportedException("not needed for the scheduled-task harness");

    public string[] GetSubKeyNames(string keyPath) =>
        throw new NotSupportedException("not needed for the scheduled-task harness");

    public bool KeyExists(string keyPath) =>
        throw new NotSupportedException("not needed for the scheduled-task harness");

    public string? PrimaryDnsV4OfActiveAdapter() =>
        throw new NotSupportedException("not needed for the scheduled-task harness");

    public bool IsSystemRestoreEnabled() =>
        throw new NotSupportedException("not needed for the scheduled-task harness");

    public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) =>
        throw new NotSupportedException("not needed for the scheduled-task harness");

    public string? ActivePowerPlanGuid() =>
        throw new NotSupportedException("not needed for the scheduled-task harness");
}
