using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A detectable read/write location. Declared ONCE per setting; carries no values. A non-empty
/// AppliesTo restricts the target to those build ranges (empty = active on every build) so one setting can
/// carry an OS-specific mechanism.</summary>
public abstract record Target(string Key)
{
    public IReadOnlyList<BuildRange> AppliesTo { get; init; } = System.Array.Empty<BuildRange>();
}

/// <summary>Registry location. Multiple <see cref="Paths"/> = a mirror (write all, read first non-null).</summary>
public sealed record RegTarget(
    string Key,
    IReadOnlyList<string> Paths,
    string? ValueName,
    RegistryValueKind Type) : Target(Key)
{
    public bool IsGroupPolicy { get; init; }
    public bool LockKeyAccess { get; init; }            // apply-only
    public int? ByteIndex { get; init; }                // REG_BINARY surgical edit
    public byte? BitMask { get; init; }                 // bit-within-byte
    public bool ByteOnly { get; init; }                 // single-byte edit, preserve rest
    public string? CompositeStringKey { get; init; }    // packed-string sub-key
    public bool PerNetworkInterface { get; init; }      // "all subkeys must match" read semantics
    public bool PerMonitor { get; init; }
}

/// <summary>powercfg setting (AC/DC). Detection reads the AC/DC value via the power query service.</summary>
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

/// <summary>Scheduled task. Detection = task enabled/disabled.</summary>
public sealed record TaskTarget(string Key, string TaskPath) : Target(Key);
