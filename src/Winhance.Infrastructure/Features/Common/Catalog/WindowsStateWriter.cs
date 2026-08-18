using System.Runtime.Versioning;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Optimize.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>The live <see cref="IStateWriter"/>: executes an apply plan against the real Windows system. Every method
/// DELEGATES to the proven WindowsRegistryService primitives / scheduled-task / powercfg / effect services - it
/// never reimplements byte logic. Registered as a singleton; sync-over-async at the writer boundary (the apply
/// funnel runs off the UI thread).</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStateWriter : IStateWriter
{
    private readonly IWindowsRegistryService _reg;
    private readonly IScheduledTaskStateService _tasks;
    private readonly IPowerCfgApplier _powerCfg;
    private readonly IPowerPlanActivationService _activation;
    private readonly ILogService _log;

    // No IPowerShellRunner / IRegImportService here any more: the two effects that used them launch a
    // process, so they are deferred to IAsyncEffectRunner instead of being blocked on at this boundary.
    public WindowsStateWriter(
        IWindowsRegistryService reg,
        IScheduledTaskStateService tasks,
        IPowerCfgApplier powerCfg,
        IPowerPlanActivationService activation,
        ILogService log)
    {
        _reg = reg;
        _tasks = tasks;
        _powerCfg = powerCfg;
        _activation = activation;
        _log = log;
    }

    // --- Registry: delegate to the primitives (CreateKey, SetValue, DeleteValue,
    //     DeleteKey, ModifyBinaryBit, ModifyBinaryByte, SetCompositeSubValue, GetSubKeyNames). ---

    public bool WriteRegistry(RegTarget target, string path, object value)
    {
        // Plain-value path: CreateKey parent first, then SetValue.
        if (!_reg.CreateKey(path))
            return false;
        return _reg.SetValue(path, target.ValueName!, value, target.Type);
    }

    public bool DeleteRegistry(RegTarget target, string path)
    {
        // A ValueName-less target encodes state as key existence, so its "off" deletes the KEY. A named value
        // deletes just the value.
        return target.ValueName == null
            ? _reg.DeleteKey(path)
            : _reg.DeleteValue(path, target.ValueName);
    }

    public bool EnsureRegistryKey(RegTarget target, string path)
    {
        // Key-existence "on" state: create the key.
        return _reg.CreateKey(path);
    }

    public bool UnlockKey(RegTarget target, string path) => _reg.UnlockRegistryKey(path);

    public bool LockKey(RegTarget target, string path) => _reg.LockRegistryKey(path);

    public bool SetRegistryBit(RegTarget target, string path, int byteIndex, byte bitMask, bool set)
    {
        // Bit branch: CreateKey first, then ModifyBinaryBit (12-byte default array handled inside).
        if (!_reg.CreateKey(path))
            return false;
        return _reg.ModifyBinaryBit(path, target.ValueName!, byteIndex, bitMask, set);
    }

    public bool SetRegistryByte(RegTarget target, string path, int byteIndex, byte value)
    {
        // Byte branch: CreateKey first, then ModifyBinaryByte.
        if (!_reg.CreateKey(path))
            return false;
        return _reg.ModifyBinaryByte(path, target.ValueName!, byteIndex, value);
    }

    public bool SetRegistryStringFlag(RegTarget target, string path, int flagMask, int absentBase, bool set)
    {
        // Flag branch: read-modify-write of a decimal-string flags value, preserving unrelated bits.
        // An absent or unparseable current value starts from the OS-default base rather than 0.
        if (!_reg.CreateKey(path))
            return false;
        long flags = _reg.GetValue(path, target.ValueName!) is string raw && long.TryParse(raw, out var parsed)
            ? parsed
            : absentBase;
        flags = set ? flags | (uint)flagMask : flags & ~(long)flagMask;
        return _reg.SetValue(path, target.ValueName!, flags.ToString(), target.Type);
    }

    public bool SetRegistryComposite(RegTarget target, string path, string compositeKey, string? subValue)
    {
        // SetCompositeSubValue does its own CreateKey + re-read-merge-write per call.
        return _reg.SetCompositeSubValue(path, target.ValueName!, compositeKey, subValue);
    }

    public bool WriteRegistryPerSubkey(RegTarget target, string parentPath, object value)
    {
        // Per-NIC / per-monitor: enumerate the parent's sub-keys LIVE per call and write the value under each.
        // No sub-keys -> false.
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
        // Per-NIC / per-monitor "absent": enumerate sub-keys LIVE per call and delete the value under each.
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

    public bool SetTask(TaskTarget target, bool enabled) =>
        _tasks.SetTaskEnabled(target.TaskPath, enabled).Success;

    // --- Powercfg ---

    public bool WritePowerCfgValue(PowerCfgTarget target, PowerContext context, int value) =>
        // Per-context write on the active scheme (battery-gated DC, commit) lives in PowerCfgApplier, where the
        // native P/Invoke already lives and is exercised by the powercfg apply-smoke.
        _powerCfg.WriteValueIndex(target, context, value);

    // --- Effects (apply-only side-effects a state runs on apply) ---

    public bool RunEffect(Effect effect)
    {
        // Routed to IAsyncEffectRunner instead; arriving here is a routing bug, and the permissive
        // default below would hide it as a success.
        if (effect.IsAsyncIo)
        {
            _log.Log(LogLevel.Error,
                $"[WindowsStateWriter] {effect.GetType().Name} must be deferred, not run on the synchronous writer");
            return false;
        }

        switch (effect)
        {
            case NativePowerEffect n:
                // CallNtPowerInformation (e.g. the hibernate toggle); status 0 is success.
                byte value = n.Value;
                return PowerProf.CallNtPowerInformation(n.InformationLevel, ref value, 1, IntPtr.Zero, 0) == 0;

            case RegistryWriteEffect w:
                // Apply-only registry write (an Action's enabled-branch value write): CreateKey then SetValue.
                if (!_reg.CreateKey(w.Path))
                    return false;
                return _reg.SetValue(w.Path, w.ValueName, w.Value, w.Kind);

            default:
                // Unknown effect: no-op success (matches ApplyExecutor's permissive default for unknown ops).
                return true;
        }
    }

    // --- Power plan (dynamic-option) activation ---

    public bool ActivatePowerPlan(string guid)
    {
        // Delegate to IPowerPlanActivationService: import-if-missing for a predefined-but-not-installed plan,
        // then activate, then InvalidateCache. A cheap guard rejects an empty/unparseable GUID up front (keeps
        // EnsureActivatedAsync's empty-GUID throw off the sync-over-async boundary). Sync-over-async at the
        // writer boundary.
        if (string.IsNullOrWhiteSpace(guid) || !Guid.TryParse(guid, out _))
        {
            _log.Log(LogLevel.Error, $"[WindowsStateWriter] ActivatePowerPlan: invalid GUID '{guid}'");
            return false;
        }

        return _activation.EnsureActivatedAsync(guid).GetAwaiter().GetResult().Success;
    }
}
