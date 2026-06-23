namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Abstracts the actual system writes so the executor is testable without touching the registry,
/// task scheduler, or power API. The real Windows implementation is provided when the engine is wired in.
/// Each method returns true on success.</summary>
public interface IStateWriter
{
    bool WriteRegistry(RegTarget target, string path, object value);
    bool DeleteRegistry(RegTarget target, string path);
    bool EnsureRegistryKey(RegTarget target, string path);
    bool SetRegistryBit(RegTarget target, string path, int byteIndex, byte bitMask, bool set);
    bool SetRegistryByte(RegTarget target, string path, int byteIndex, byte value);
    bool SetRegistryComposite(RegTarget target, string path, string compositeKey, string? subValue);
    bool WriteRegistryPerSubkey(RegTarget target, string parentPath, object value);
    bool DeleteRegistryPerSubkey(RegTarget target, string parentPath);
    bool SetTask(TaskTarget target, bool enabled);

    /// <summary>Writes the AC or DC value index for the active scheme; the real Windows writer uses
    /// PowerWriteAC/DCValueIndex.</summary>
    bool WritePowerCfgValue(PowerCfgTarget target, PowerContext context, int value);

    bool RunEffect(Effect effect);
}
