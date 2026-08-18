using Microsoft.Win32;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

// Declared ONCE per setting and carries no values; AppliesTo restricts it to build ranges so one setting can carry
// an OS-specific mechanism.
public abstract record Target(string Key)
{
    public IReadOnlyList<BuildRange> AppliesTo { get; init; } = System.Array.Empty<BuildRange>();
}

// Multiple Paths = a mirror (write all, read the first non-null).
public sealed record RegTarget(
    string Key,
    IReadOnlyList<string> Paths,
    string? ValueName,
    RegistryValueKind Type) : Target(Key)
{
    public bool IsGroupPolicy { get; init; }
    public bool ApplyOnly { get; init; }                // written on apply but NOT read on detect (a sync/mirror key)
    public int? LockWhenValue { get; init; }            // apply-only: ACL-lock the key after writing THIS value (null = never lock)
    public int? ByteIndex { get; init; }                // REG_BINARY surgical edit
    public byte? BitMask { get; init; }                 // bit-within-byte
    public bool ByteOnly { get; init; }                 // single-byte edit, preserve rest
    public string? CompositeStringKey { get; init; }    // packed-string sub-key
    public int? StringFlagMask { get; init; }           // decimal-string flags: bit test/edit
    public int StringFlagAbsentBase { get; init; }      // flags assumed when the value is absent (OS default)
    public bool PerNetworkInterface { get; init; }      // "all subkeys must match" read semantics
    public bool PerMonitor { get; init; }
}

public sealed record PowerCfgTarget(
    string Key,
    string SubgroupGuid,
    string SettingGuid,
    PowerModeSupport Mode) : Target(Key)
{
    public string? Units { get; init; }
    public RegTarget? EnablementKey { get; init; }      // unhide a hidden powercfg setting before query
    public bool CheckForHardwareControl { get; init; }
}

public sealed record TaskTarget(string Key, string TaskPath) : Target(Key);
