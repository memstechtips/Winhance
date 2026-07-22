namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Abstracts the actual system writes so the executor is testable without touching the registry,
/// task scheduler, or power API. The real Windows implementation is provided when the engine is wired in.
/// Each method returns true on success.</summary>
public interface IStateWriter
{
    bool WriteRegistry(RegTarget target, string path, object value);
    bool DeleteRegistry(RegTarget target, string path);
    bool EnsureRegistryKey(RegTarget target, string path);

    /// <summary>Restore writable permissions on a key before writing it (old UnlockRegistryKey).</summary>
    bool UnlockKey(RegTarget target, string path);

    /// <summary>ACL-lock a key read-only for SYSTEM after writing its protective value (old LockRegistryKey).</summary>
    bool LockKey(RegTarget target, string path);
    bool SetRegistryBit(RegTarget target, string path, int byteIndex, byte bitMask, bool set);
    bool SetRegistryByte(RegTarget target, string path, int byteIndex, byte value);
    bool SetRegistryStringFlag(RegTarget target, string path, int flagMask, int absentBase, bool set);
    bool SetRegistryComposite(RegTarget target, string path, string compositeKey, string? subValue);
    bool WriteRegistryPerSubkey(RegTarget target, string parentPath, object value);
    bool DeleteRegistryPerSubkey(RegTarget target, string parentPath);
    bool SetTask(TaskTarget target, bool enabled);

    /// <summary>Writes the AC or DC value index for the active scheme; the real Windows writer uses
    /// PowerWriteAC/DCValueIndex.</summary>
    bool WritePowerCfgValue(PowerCfgTarget target, PowerContext context, int value);

    /// <summary>Activate a power scheme by GUID (old PowerService SetActiveScheme). Returns true on success.</summary>
    bool ActivatePowerPlan(string guid);

    bool RunEffect(Effect effect);
}
