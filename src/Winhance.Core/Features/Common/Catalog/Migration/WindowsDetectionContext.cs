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

    public object? GetValue(string keyPath, string? valueName) => _reg.GetValue(keyPath, valueName ?? "");

    public string[] GetSubKeyNames(string keyPath) => _reg.GetSubKeyNames(keyPath);

    public string? PrimaryDnsV4OfActiveAdapter() =>
        throw new NotSupportedException("not needed for the toggle harness");

    public bool IsSystemRestoreEnabled() =>
        throw new NotSupportedException("not needed for the toggle harness");
}
