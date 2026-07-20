namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A single declarative write the apply engine will perform. Produced by the plan builder,
/// executed by the executor against an <see cref="IStateWriter"/>.</summary>
public abstract record ApplyOp;

/// <summary>Write a concrete value to one registry path of a target (mirror targets emit one per path).</summary>
public sealed record RegistryWriteOp(RegTarget Target, string Path, object Value) : ApplyOp;

/// <summary>Delete the target's value from one registry path (the "absent" state).</summary>
public sealed record RegistryDeleteOp(RegTarget Target, string Path) : ApplyOp;

/// <summary>Ensure the key/value exists at one registry path (key-existence "on" state).</summary>
public sealed record RegistryEnsureKeyOp(RegTarget Target, string Path) : ApplyOp;

/// <summary>Restore writable permissions on a registry key before writing it (via UnlockRegistryKey; the inverse
/// of the ACL lock). Emitted before the value write for a lockable target (RegTarget.LockWhenValue).</summary>
public sealed record RegistryUnlockKeyOp(RegTarget Target, string Path) : ApplyOp;

/// <summary>ACL-lock a registry key to read-only for SYSTEM after writing its protective value, so Windows cannot
/// revert it (via LockRegistryKey). Emitted only when the written value equals the target's LockWhenValue.</summary>
public sealed record RegistryLockKeyOp(RegTarget Target, string Path) : ApplyOp;

/// <summary>Set or clear a single bit within one byte of a REG_BINARY value, preserving the other bytes
/// (the surgical bit edit, via ModifyBinaryBit).</summary>
public sealed record RegistryBitSetOp(RegTarget Target, string Path, int ByteIndex, byte BitMask, bool Set) : ApplyOp;

/// <summary>Overwrite a single byte of a REG_BINARY value, preserving the other bytes (the surgical byte
/// edit, via ModifyBinaryByte).</summary>
public sealed record RegistryByteSetOp(RegTarget Target, string Path, int ByteIndex, byte Value) : ApplyOp;

/// <summary>Set (or, when <see cref="SubValue"/> is null, remove) one sub-key inside a packed ";"-delimited
/// "key=value" REG_SZ value, preserving the other sub-keys (the read-merge-write for a
/// CompositeStringKey setting).</summary>
public sealed record RegistryCompositeSetOp(RegTarget Target, string Path, string CompositeKey, string? SubValue) : ApplyOp;

/// <summary>Write one value to <see cref="Target"/>'s ValueName under EVERY sub-key of <see cref="ParentPath"/>
/// (the per-network-interface / per-monitor "enumerate sub-keys and write each"). The sub-key
/// enumeration is deferred to the writer; this op carries the per-sub-key write intent.</summary>
public sealed record RegistryPerSubkeyWriteOp(RegTarget Target, string ParentPath, object Value) : ApplyOp;

/// <summary>Delete <see cref="Target"/>'s ValueName under EVERY sub-key of <see cref="ParentPath"/> (the
/// per-NIC/per-monitor "absent" state). Sub-key enumeration is deferred to the writer.</summary>
public sealed record RegistryPerSubkeyDeleteOp(RegTarget Target, string ParentPath) : ApplyOp;

/// <summary>Enable or disable a scheduled task.</summary>
public sealed record TaskSetOp(TaskTarget Target, bool Enabled) : ApplyOp;

/// <summary>Write one AC or DC value index for a powercfg setting on the active scheme.</summary>
public sealed record PowerCfgSetOp(PowerCfgTarget Target, PowerContext Context, int Value) : ApplyOp;

/// <summary>Run one apply-only effect (script / .reg import / native power write).</summary>
public sealed record EffectOp(Effect Effect) : ApplyOp;

/// <summary>Activate a power scheme by GUID on the live system (the dynamic-option power-plan apply). The writer
/// activates an installed scheme directly; importing a predefined-but-not-installed plan before activating is
/// handled by the activation service. Built by the resolver and routed through the engine.</summary>
public sealed record PowerPlanActivateOp(string Guid) : ApplyOp;
