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

/// <summary>Set or clear a single bit within one byte of a REG_BINARY value, preserving the other bytes
/// (the surgical bit edit the old apply did via ModifyBinaryBit).</summary>
public sealed record RegistryBitSetOp(RegTarget Target, string Path, int ByteIndex, byte BitMask, bool Set) : ApplyOp;

/// <summary>Overwrite a single byte of a REG_BINARY value, preserving the other bytes (the surgical byte
/// edit the old apply did via ModifyBinaryByte).</summary>
public sealed record RegistryByteSetOp(RegTarget Target, string Path, int ByteIndex, byte Value) : ApplyOp;

/// <summary>Set (or, when <see cref="SubValue"/> is null, remove) one sub-key inside a packed ";"-delimited
/// "key=value" REG_SZ value, preserving the other sub-keys (the read-merge-write the old apply did for a
/// CompositeStringKey setting).</summary>
public sealed record RegistryCompositeSetOp(RegTarget Target, string Path, string CompositeKey, string? SubValue) : ApplyOp;

/// <summary>Write one value to <see cref="Target"/>'s ValueName under EVERY sub-key of <see cref="ParentPath"/>
/// (the per-network-interface / per-monitor "enumerate sub-keys and write each" the old apply did). The sub-key
/// enumeration is deferred to the writer; this op carries the per-sub-key write intent.</summary>
public sealed record RegistryPerSubkeyWriteOp(RegTarget Target, string ParentPath, object Value) : ApplyOp;

/// <summary>Delete <see cref="Target"/>'s ValueName under EVERY sub-key of <see cref="ParentPath"/> (the
/// per-NIC/per-monitor "absent" state). Sub-key enumeration is deferred to the writer.</summary>
public sealed record RegistryPerSubkeyDeleteOp(RegTarget Target, string ParentPath) : ApplyOp;

/// <summary>Enable or disable a scheduled task.</summary>
public sealed record TaskSetOp(TaskTarget Target, bool Enabled) : ApplyOp;

/// <summary>Run one apply-only effect (script / .reg import / native power write).</summary>
public sealed record EffectOp(Effect Effect) : ApplyOp;
