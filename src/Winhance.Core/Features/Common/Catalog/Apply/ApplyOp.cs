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

/// <summary>Enable or disable a scheduled task.</summary>
public sealed record TaskSetOp(TaskTarget Target, bool Enabled) : ApplyOp;

/// <summary>Run one apply-only effect (script / .reg import / native power write).</summary>
public sealed record EffectOp(Effect Effect) : ApplyOp;
