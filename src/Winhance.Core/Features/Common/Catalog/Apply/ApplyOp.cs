namespace Winhance.Core.Features.Common.Catalog;

public abstract record ApplyOp;

public sealed record RegistryWriteOp(RegTarget Target, string Path, object Value) : ApplyOp;

public sealed record RegistryDeleteOp(RegTarget Target, string Path) : ApplyOp;

public sealed record RegistryEnsureKeyOp(RegTarget Target, string Path) : ApplyOp;

public sealed record RegistryUnlockKeyOp(RegTarget Target, string Path) : ApplyOp;

// Read-only for SYSTEM so Windows cannot revert the value; only emitted when the written value equals LockWhenValue.
public sealed record RegistryLockKeyOp(RegTarget Target, string Path) : ApplyOp;

public sealed record RegistryBitSetOp(RegTarget Target, string Path, int ByteIndex, byte BitMask, bool Set) : ApplyOp;

public sealed record RegistryByteSetOp(RegTarget Target, string Path, int ByteIndex, byte Value) : ApplyOp;

public sealed record RegistryStringFlagSetOp(RegTarget Target, string Path, int FlagMask, int AbsentBase, bool Set) : ApplyOp;

public sealed record RegistryCompositeSetOp(RegTarget Target, string Path, string CompositeKey, string? SubValue) : ApplyOp;

public sealed record RegistryPerSubkeyWriteOp(RegTarget Target, string ParentPath, object Value) : ApplyOp;

public sealed record RegistryPerSubkeyDeleteOp(RegTarget Target, string ParentPath) : ApplyOp;

public sealed record TaskSetOp(TaskTarget Target, bool Enabled) : ApplyOp;

public sealed record PowerCfgSetOp(PowerCfgTarget Target, PowerContext Context, int Value) : ApplyOp;

public sealed record EffectOp(Effect Effect) : ApplyOp;

// Importing a predefined-but-not-installed plan first is the activation service's job, not the writer's.
public sealed record PowerPlanActivateOp(string Guid) : ApplyOp;
