using System;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: the minimal <see cref="IDetectionContext"/> the system-restore harness
/// needs. It carries the pre-fetched "is System Restore on for C:" value (the harness reads it up front via
/// <c>ISystemRestoreService.IsEnabledForC</c>); the registry and network members are never reached by the
/// <see cref="SystemRestoreDetector"/> and throw if called. Deleted once the migration is complete.</summary>
public sealed class SystemRestoreDetectionContext : IDetectionContext
{
    private readonly bool _enabled;

    public SystemRestoreDetectionContext(bool enabled) => _enabled = enabled;

    public bool IsSystemRestoreEnabled() => _enabled;

    public object? GetValue(string keyPath, string? valueName) =>
        throw new NotSupportedException("not needed for the system-restore harness");

    public string[] GetSubKeyNames(string keyPath) =>
        throw new NotSupportedException("not needed for the system-restore harness");

    public bool KeyExists(string keyPath) =>
        throw new NotSupportedException("not needed for the system-restore harness");

    public string? PrimaryDnsV4OfActiveAdapter() =>
        throw new NotSupportedException("not needed for the system-restore harness");

    public bool? ScheduledTaskEnabled(string taskPath) =>
        throw new NotSupportedException("not needed for the system-restore harness");
}
