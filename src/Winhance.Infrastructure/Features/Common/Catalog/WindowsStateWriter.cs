using System;
using System.Runtime.Versioning;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;

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
    private readonly IPowerShellRunner _powerShell;
    private readonly IRegImportService _regImport;
    private readonly IPowerSchemeOperations _schemes;
    private readonly ILogService _log;

    public WindowsStateWriter(
        IWindowsRegistryService reg,
        IScheduledTaskService tasks,
        IPowerCfgApplier powerCfg,
        IPowerShellRunner powerShell,
        IRegImportService regImport,
        IPowerSchemeOperations schemes,
        ILogService log)
    {
        _reg = reg;
        _tasks = tasks;
        _powerCfg = powerCfg;
        _powerShell = powerShell;
        _regImport = regImport;
        _schemes = schemes;
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

    // --- Effects (apply-only side-effects a state runs on apply) ---

    public bool RunEffect(Effect effect)
    {
        switch (effect)
        {
            case ScriptEffect s:
                // Old apply runs the script in-memory and does NOT track its result (it never adds to
                // failedOperations). RunContext is carried for fidelity but the old apply does not pass it.
                // Sync-over-async at the writer boundary.
                _powerShell.RunScriptInMemoryAsync(s.Script).GetAwaiter().GetResult();
                return true;

            case RegContentEffect r:
                // .reg import via the OTS-aware dance (throws on a file-system / process exception, like the old
                // apply; a non-zero reg.exe exit is logged, not treated as failure).
                _regImport.RunRegImportAsync(r.Content).GetAwaiter().GetResult();
                return true;

            case NativePowerEffect n:
                // CallNtPowerInformation (e.g. the hibernate toggle); the old apply treats status 0 as success.
                byte value = n.Value;
                return PowerProf.CallNtPowerInformation(n.InformationLevel, ref value, 1, IntPtr.Zero, 0) == 0;

            case RegistryWriteEffect w:
                // Apply-only registry write (an Action's enabled-branch value write): CreateKey then SetValue,
                // matching the old enabled-branch ApplySetting.
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
        // Slice 8a: activate an INSTALLED scheme by GUID (old PowerService.SetActivePowerPlanAsync ->
        // IPowerSchemeOperations.SetActiveScheme). Importing a predefined-but-not-installed plan before activating
        // (the old ApplyPowerPlanByGuidAsync import-if-missing branch) lands in Slice 8b's shared importer; here a
        // not-installed GUID simply fails the native activate and returns false. NOT reached at runtime until 8b
        // flips the resolver + removes the PowerService special handler.
        if (string.IsNullOrWhiteSpace(guid) || !Guid.TryParse(guid, out var schemeGuid))
        {
            _log.Log(LogLevel.Error, $"[WindowsStateWriter] ActivatePowerPlan: invalid GUID '{guid}'");
            return false;
        }

        var rc = _schemes.SetActiveScheme(schemeGuid);
        if (rc != PowerProf.ERROR_SUCCESS)
        {
            _log.Log(LogLevel.Warning, $"[WindowsStateWriter] SetActiveScheme failed for {schemeGuid} (code {rc})");
            return false;
        }
        return true;
    }
}
