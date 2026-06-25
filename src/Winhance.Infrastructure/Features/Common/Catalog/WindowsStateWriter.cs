using System;
using System.Runtime.Versioning;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>The live <see cref="IStateWriter"/>: executes an apply plan against the real Windows system. Every method
/// DELEGATES to the proven WindowsRegistryService primitives (exposed in Phase 6.4 Slice 1) / scheduled-task /
/// powercfg / effect services - it never reimplements byte logic - so the new apply path performs the exact writes
/// the old ApplySetting did. Registered as a singleton; sync-over-async at the writer boundary (the apply funnel
/// runs off the UI thread). Powercfg + effects land in Slice 2b/2c; the writer is not wired into the funnel until
/// Slice 4.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStateWriter : IStateWriter
{
    private readonly IWindowsRegistryService _reg;
    private readonly IScheduledTaskService _tasks;
    private readonly IPowerCfgApplier _powerCfg;
    private readonly ILogService _log;

    public WindowsStateWriter(
        IWindowsRegistryService reg,
        IScheduledTaskService tasks,
        IPowerCfgApplier powerCfg,
        ILogService log)
    {
        _reg = reg;
        _tasks = tasks;
        _powerCfg = powerCfg;
        _log = log;
    }

    // --- Registry: delegate to the primitives exposed in Phase 6.4 Slice 1 (CreateKey, SetValue, DeleteValue,
    //     DeleteKey, ModifyBinaryBit, ModifyBinaryByte, SetCompositeSubValue, GetSubKeyNames). ---

    public bool WriteRegistry(RegTarget target, string path, object value)
    {
        // Old apply plain-value path: CreateKey parent first, then SetValue (WindowsRegistryService.ApplySetting).
        if (!_reg.CreateKey(path))
            return false;
        return _reg.SetValue(path, target.ValueName!, value, target.Type);
    }

    public bool DeleteRegistry(RegTarget target, string path)
    {
        // A ValueName-less target encodes state as key existence, so its "off" deletes the KEY (old ApplySetting
        // DeleteKey branch). A named value deletes just the value (old ApplySetting null-value DeleteValue branch).
        return target.ValueName == null
            ? _reg.DeleteKey(path)
            : _reg.DeleteValue(path, target.ValueName);
    }

    public bool EnsureRegistryKey(RegTarget target, string path)
    {
        // Key-existence "on" state: create the key (old ApplySetting ValueName-null enable -> CreateKey).
        return _reg.CreateKey(path);
    }

    public bool UnlockKey(RegTarget target, string path) => _reg.UnlockRegistryKey(path);

    public bool LockKey(RegTarget target, string path) => _reg.LockRegistryKey(path);

    public bool SetRegistryBit(RegTarget target, string path, int byteIndex, byte bitMask, bool set)
    {
        // Old apply bit branch: CreateKey first, then ModifyBinaryBit (12-byte default array handled inside).
        if (!_reg.CreateKey(path))
            return false;
        return _reg.ModifyBinaryBit(path, target.ValueName!, byteIndex, bitMask, set);
    }

    public bool SetRegistryByte(RegTarget target, string path, int byteIndex, byte value)
    {
        // Old apply byte branch: CreateKey first, then ModifyBinaryByte.
        if (!_reg.CreateKey(path))
            return false;
        return _reg.ModifyBinaryByte(path, target.ValueName!, byteIndex, value);
    }

    public bool SetRegistryComposite(RegTarget target, string path, string compositeKey, string? subValue)
    {
        // SetCompositeSubValue does its own CreateKey + re-read-merge-write per call (extracted in Slice 1).
        return _reg.SetCompositeSubValue(path, target.ValueName!, compositeKey, subValue);
    }

    public bool WriteRegistryPerSubkey(RegTarget target, string parentPath, object value)
    {
        // Per-NIC / per-monitor: enumerate the parent's sub-keys LIVE per call and write the value under each
        // (old ApplySetting expands every sub-key then writes). No sub-keys -> false, matching the old apply.
        var subKeys = _reg.GetSubKeyNames(parentPath);
        if (subKeys.Length == 0)
        {
            _log.Log(LogLevel.Warning, $"[WindowsStateWriter] No subkeys under '{parentPath}' for per-subkey write");
            return false;
        }

        var allSucceeded = true;
        foreach (var subKey in subKeys)
        {
            var subPath = $@"{parentPath}\{subKey}";
            if (!_reg.CreateKey(subPath) || !_reg.SetValue(subPath, target.ValueName!, value, target.Type))
                allSucceeded = false;
        }
        return allSucceeded;
    }

    public bool DeleteRegistryPerSubkey(RegTarget target, string parentPath)
    {
        // Per-NIC / per-monitor "absent": enumerate sub-keys LIVE per call and delete the value under each
        // (old ApplySetting per-sub-key apply with a null value -> DeleteValue under each sub-key).
        var subKeys = _reg.GetSubKeyNames(parentPath);
        if (subKeys.Length == 0)
        {
            _log.Log(LogLevel.Warning, $"[WindowsStateWriter] No subkeys under '{parentPath}' for per-subkey delete");
            return false;
        }

        var allSucceeded = true;
        foreach (var subKey in subKeys)
        {
            var subPath = $@"{parentPath}\{subKey}";
            if (!_reg.DeleteValue(subPath, target.ValueName!))
                allSucceeded = false;
        }
        return allSucceeded;
    }

    // --- Scheduled task ---

    public bool SetTask(TaskTarget target, bool enabled)
    {
        // Old apply (SettingOperationExecutor): Enable/DisableTaskAsync. Sync-over-async at the writer boundary.
        var result = enabled
            ? _tasks.EnableTaskAsync(target.TaskPath).GetAwaiter().GetResult()
            : _tasks.DisableTaskAsync(target.TaskPath).GetAwaiter().GetResult();
        return result.Success;
    }

    // --- Powercfg ---

    public bool WritePowerCfgValue(PowerCfgTarget target, PowerContext context, int value) =>
        // Per-context write on the active scheme (battery-gated DC, commit) lives in PowerCfgApplier, where the
        // native P/Invoke already lives and is exercised by the powercfg apply-smoke. Sync-over-async at the boundary.
        _powerCfg.WriteValueIndexAsync(target, context, value).GetAwaiter().GetResult();

    // --- Effects: Phase 6.4 Slice 2c. The writer is NOT wired into the apply funnel until Slice 4,
    //     so this throwing placeholder is unreachable in production until then. ---

    public bool RunEffect(Effect effect) =>
        throw new NotSupportedException(
            "WindowsStateWriter.RunEffect is implemented in Phase 6.4 Slice 2c; the writer is not wired into the apply funnel until Slice 4.");
}
