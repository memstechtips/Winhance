using System;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: the minimal <see cref="IDetectionContext"/> the power-plan equivalence
/// harness needs. It carries one pre-fetched active-power-scheme GUID string (the harness reads it from the real
/// power query up front, since the context API is synchronous); the registry, network, scheduled-task,
/// system-restore and powercfg-value members are never reached by the <see cref="PowerPlanDetector"/> and throw
/// if called. Deleted once the migration is complete.</summary>
public sealed class PowerPlanDetectionContext : IDetectionContext
{
    private readonly string? _activePlanGuid;

    public PowerPlanDetectionContext(string? activePlanGuid) => _activePlanGuid = activePlanGuid;

    public WinBuild CurrentBuild => new(int.MaxValue); // power-plan selection is not build-gated

    public string? ActivePowerPlanGuid() => _activePlanGuid;

    public object? GetValue(string keyPath, string? valueName) =>
        throw new NotSupportedException("not needed for the power-plan harness");

    public string[] GetSubKeyNames(string keyPath) =>
        throw new NotSupportedException("not needed for the power-plan harness");

    public bool KeyExists(string keyPath) =>
        throw new NotSupportedException("not needed for the power-plan harness");

    public string? PrimaryDnsV4OfActiveAdapter() =>
        throw new NotSupportedException("not needed for the power-plan harness");

    public bool IsSystemRestoreEnabled() =>
        throw new NotSupportedException("not needed for the power-plan harness");

    public bool? ScheduledTaskEnabled(string taskPath) =>
        throw new NotSupportedException("not needed for the power-plan harness");

    public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) =>
        throw new NotSupportedException("not needed for the power-plan harness");
}
