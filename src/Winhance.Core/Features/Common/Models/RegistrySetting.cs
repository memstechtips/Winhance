using Microsoft.Win32;

namespace Winhance.Core.Features.Common.Models;

public sealed record RegistrySetting
{
    public required string KeyPath { get; init; }
    public string? ValueName { get; init; }
    public required object? RecommendedValue { get; init; }
    public required object? DefaultValue { get; init; }
    public object?[]? EnabledValue { get; init; }
    public object?[]? DisabledValue { get; init; }
    public required RegistryValueKind ValueType { get; init; }
    public bool IsPrimary { get; init; } = false;
    public int? BinaryByteIndex { get; init; }
    public bool ModifyByteOnly { get; init; } = false;
    public byte? BitMask { get; init; }
    public string? CompositeStringKey { get; init; }
    public bool ApplyPerNetworkInterface { get; init; } = false;
    public bool ApplyPerMonitor { get; init; } = false;
    public bool IsGroupPolicy { get; init; } = false;
    public bool LockKeyAccess { get; init; } = false;
}
