using System;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: the minimal <see cref="IDetectionContext"/> the toggle equivalence
/// harness needs. Registry reads delegate to <see cref="IWindowsRegistryService"/>; the network and
/// system-restore members are never reached by pure registry toggles and throw if called. The real,
/// full context is wired up later for the Tier-2 detectors. Deleted once the migration is complete.</summary>
public sealed class WindowsDetectionContext : IDetectionContext
{
    private readonly IWindowsRegistryService _reg;

    public WindowsDetectionContext(IWindowsRegistryService reg) => _reg = reg;

    public WinBuild CurrentBuild
    {
        get
        {
            const string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            int build = int.TryParse(_reg.GetValue(key, "CurrentBuildNumber")?.ToString(), out var b) ? b : 0;
            int ubr = int.TryParse(_reg.GetValue(key, "UBR")?.ToString(), out var r) ? r : 0;
            return new WinBuild(build, ubr);
        }
    }

    public object? GetValue(string keyPath, string? valueName) => _reg.GetValue(keyPath, valueName ?? "");

    public string[] GetSubKeyNames(string keyPath) => _reg.GetSubKeyNames(keyPath);

    public bool KeyExists(string keyPath) => _reg.KeyExists(keyPath);

    public string? PrimaryDnsV4OfActiveAdapter() =>
        throw new NotSupportedException("not needed for the toggle harness");

    public bool IsSystemRestoreEnabled() =>
        throw new NotSupportedException("not needed for the toggle harness");

    public bool? ScheduledTaskEnabled(string taskPath) =>
        throw new NotSupportedException("not needed for the registry harness; tasks use their own context");

    public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) =>
        throw new NotSupportedException("Powercfg reads are not needed for the registry harness.");
}
