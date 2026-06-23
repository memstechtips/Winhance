using System;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: the minimal <see cref="IDetectionContext"/> the powercfg equivalence
/// harness needs. It carries one pre-fetched AC/DC value pair (the harness reads it async up front, since the
/// context API is synchronous); the registry, network, scheduled-task and system-restore members are never
/// reached by a pure powercfg setting and throw if called. Deleted once the migration is complete.</summary>
public sealed class PowerCfgDetectionContext : IDetectionContext
{
    private readonly int? _ac;
    private readonly int? _dc;

    public PowerCfgDetectionContext(int? ac, int? dc)
    {
        _ac = ac;
        _dc = dc;
    }

    public WinBuild CurrentBuild => new(int.MaxValue); // powercfg settings are not build-gated

    public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) =>
        context == PowerContext.DC ? _dc : _ac;

    public object? GetValue(string keyPath, string? valueName) =>
        throw new NotSupportedException("not needed for the powercfg harness");

    public string[] GetSubKeyNames(string keyPath) =>
        throw new NotSupportedException("not needed for the powercfg harness");

    public bool KeyExists(string keyPath) =>
        throw new NotSupportedException("not needed for the powercfg harness");

    public string? PrimaryDnsV4OfActiveAdapter() =>
        throw new NotSupportedException("not needed for the powercfg harness");

    public bool IsSystemRestoreEnabled() =>
        throw new NotSupportedException("not needed for the powercfg harness");

    public bool? ScheduledTaskEnabled(string taskPath) =>
        throw new NotSupportedException("not needed for the powercfg harness");
}
