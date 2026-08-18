namespace Winhance.Core.Features.Common.Catalog;

public interface IStateWriter
{
    bool WriteRegistry(RegTarget target, string path, object value);
    bool DeleteRegistry(RegTarget target, string path);
    bool EnsureRegistryKey(RegTarget target, string path);

    bool UnlockKey(RegTarget target, string path);

    bool LockKey(RegTarget target, string path);
    bool SetRegistryBit(RegTarget target, string path, int byteIndex, byte bitMask, bool set);
    bool SetRegistryByte(RegTarget target, string path, int byteIndex, byte value);
    bool SetRegistryStringFlag(RegTarget target, string path, int flagMask, int absentBase, bool set);
    bool SetRegistryComposite(RegTarget target, string path, string compositeKey, string? subValue);
    bool WriteRegistryPerSubkey(RegTarget target, string parentPath, object value);
    bool DeleteRegistryPerSubkey(RegTarget target, string parentPath);
    bool SetTask(TaskTarget target, bool enabled);

    bool WritePowerCfgValue(PowerCfgTarget target, PowerContext context, int value);

    bool ActivatePowerPlan(string guid);

    bool RunEffect(Effect effect);
}
