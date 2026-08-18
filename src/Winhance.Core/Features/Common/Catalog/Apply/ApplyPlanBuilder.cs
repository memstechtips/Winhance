namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Turns "apply state &lt;label&gt; of &lt;setting&gt;" into an ordered list of declarative write ops - the
/// forward direction of target-by-state. Pure; no I/O. Registry, scheduled-task, powercfg, and per-state
/// effect targets are all handled here (a powercfg target emits a PowerCfgSetOp per AC/DC context).
/// </summary>
public static class ApplyPlanBuilder
{
    public static IReadOnlyList<ApplyOp> Build(Setting setting, string stateLabel, WinBuild? build = null, bool reset = false)
    {
        var state = setting.States.FirstOrDefault(s => s.Label == stateLabel)
            ?? throw new ArgumentException($"No state labelled '{stateLabel}' on setting '{setting.Id}'.", nameof(stateLabel));
        return Build(setting, state, build, reset);
    }

    /// <summary>Build the apply plan for an EXPLICIT state (not looked up by label). The label overload above resolves
    /// the state then delegates here; the custom-state path (<see cref="BuildRegistryCustomState"/>) synthesizes a
    /// transient state and calls this directly.</summary>
    public static IReadOnlyList<ApplyOp> Build(Setting setting, SettingState state, WinBuild? build = null, bool reset = false)
    {
        var ops = new List<ApplyOp>();

        // A state that IS this build's Windows default writes its ResetSet on ANY apply, not just the reset
        // button - "put this how Windows has it" cannot depend on which button asked. Where one state carries
        // both roles, Apply Recommended and the per-card quick-set would otherwise stamp values back onto the
        // targets Apply Windows Defaults had just deleted.
        bool resetWrites = reset || (build is { } roleBuild
            ? state.HasRole(RoleKind.WindowsDefault, roleBuild)
            : state.HasRole(RoleKind.WindowsDefault));

        // A setting that applies via a .reg import does NOT write its registry targets - those are detect-only;
        // the import is the apply.
        bool appliesViaRegContent = setting.States.Any(s => s.Effects.OfType<RegContentEffect>().Any());

        foreach (var target in setting.Targets)
        {
            if (build is { } b && target.AppliesTo.Count > 0 && !target.AppliesTo.Any(r => r.Contains(b)))
                continue; // target not live on this build

            switch (target)
            {
                case RegTarget reg:
                    if (appliesViaRegContent)
                        break; // detect-only: the .reg import (an Effect) is the apply
                    // A state's ResetSet overrides its Set per target (the [1,null] Explorer targets detect
                    // "1-or-absent" but DELETE); a target absent from ResetSet falls back to its normal Set write.
                    StateValue sv;
                    if (resetWrites && state.ResetSet is { } resetSet && resetSet.TryGetValue(reg.Key, out var resetSv))
                        sv = resetSv;
                    else if (!state.Set.TryGetValue(reg.Key, out var setSv))
                        continue; // state doesn't cover this target (e.g. a fallback's partial Set) - leave it alone
                    else
                        sv = setSv;
                    foreach (var path in reg.Paths)
                    {
                        if (reg.PerNetworkInterface || reg.PerMonitor)
                        {
                            // Expand-and-write-each: enumerate the parent key's sub-keys and apply the same write to
                            // each. Enumeration is deferred to the writer; emit the per-sub-key intent.
                            if (sv.DeleteOnWrite)
                                ops.Add(new RegistryPerSubkeyDeleteOp(reg, path));
                            else if (sv.WritePayload is { } subPayload)
                                ops.Add(new RegistryPerSubkeyWriteOp(reg, path, subPayload));
                        }
                        else if (reg.CompositeStringKey is { } compositeKey)
                        {
                            // Set (or remove, when the payload is null) one sub-key inside the packed string;
                            // the read-merge-write of the other sub-keys happens in the writer.
                            ops.Add(new RegistryCompositeSetOp(reg, path, compositeKey, sv.WritePayload?.ToString()));
                        }
                        else if (reg.BitMask is { } bitMask && reg.ByteIndex is { } bitByteIndex)
                        {
                            // Surgical bit edit within a REG_BINARY byte: the payload's truthiness is the bit state.
                            bool setBit = sv.WritePayload is { } bp && Convert.ToBoolean(bp);
                            ops.Add(new RegistryBitSetOp(reg, path, bitByteIndex, bitMask, setBit));
                        }
                        else if (reg.ByteOnly && reg.ByteIndex is { } byteIndex)
                        {
                            // Single-byte overwrite within a REG_BINARY value: the payload is the byte to write.
                            byte value = sv.WritePayload is { } yp ? Convert.ToByte(yp) : (byte)0;
                            ops.Add(new RegistryByteSetOp(reg, path, byteIndex, value));
                        }
                        else if (reg.StringFlagMask is { } flagMask)
                        {
                            // Surgical flag edit within a decimal-string flags value: payload truthiness = flag state.
                            bool setFlag = sv.WritePayload is { } fp && Convert.ToBoolean(fp);
                            ops.Add(new RegistryStringFlagSetOp(reg, path, flagMask, reg.StringFlagAbsentBase, setFlag));
                        }
                        else
                        {
                            // Plain value path. A lockable target (LockWhenValue set) is unlocked before the write
                            // and re-locked after, but only when the written value is the protective LockWhenValue.
                            if (reg.LockWhenValue is not null)
                                ops.Add(new RegistryUnlockKeyOp(reg, path));
                            if (sv.DeleteOnWrite)
                                ops.Add(new RegistryDeleteOp(reg, path));
                            else if (sv.WritePayload is { } payload)
                            {
                                ops.Add(new RegistryWriteOp(reg, path, payload));
                                if (reg.LockWhenValue is { } lockVal && Convert.ToInt64(payload) == lockVal)
                                    ops.Add(new RegistryLockKeyOp(reg, path));
                            }
                            else if (sv.AcceptsAnyPresent)
                                ops.Add(new RegistryEnsureKeyOp(reg, path)); // Exists: ensure key/value present
                            // else: nothing concrete to write (defensive; the validator should prevent this)
                        }
                    }
                    break;

                case TaskTarget task:
                    if (state.Set.TryGetValue(task.Key, out var tv) && tv.WritePayload is { } tval)
                        ops.Add(new TaskSetOp(task, Convert.ToBoolean(tval)));
                    break;

                case PowerCfgTarget pc:
                    // A powercfg SELECTION applies the chosen option's int value to BOTH the AC and DC contexts
                    // (the symmetric single-index semantics). Pull the StateValue for this target the same way the
                    // RegTarget branch does (by the target's Key), then cast its WritePayload to the option's int.
                    if (state.Set.TryGetValue(pc.Key, out var pv) && pv.WritePayload is { } powerPayload)
                    {
                        int value = Convert.ToInt32(powerPayload);
                        ops.Add(new PowerCfgSetOp(pc, PowerContext.AC, value));
                        ops.Add(new PowerCfgSetOp(pc, PowerContext.DC, value));
                    }
                    break;
            }
        }

        // Effects run after the registry/task state is in place.
        foreach (var effect in state.Effects)
            ops.Add(new EffectOp(effect));

        return ops;
    }

    /// <summary>Apply plan for a registry-selection CUSTOM state (config-import CustomStateValues: a "Custom" /
    /// no-option state re-applied as raw per-ValueName registry values). Synthesizes a transient state whose Set maps
    /// each RegTarget (whose ValueName is present in the dict) to a WRITE (Of) or a DELETE (Absent, for a null captured
    /// value), then runs the normal per-target apply via <see cref="Build(Setting, SettingState, WinBuild?, bool)"/>.
    /// Only registry targets are written; the resolver gates this to pure registry selections (no
    /// effects/tasks/powercfg), so the transient state carries no Effects.</summary>
    public static IReadOnlyList<ApplyOp> BuildRegistryCustomState(Setting setting, IReadOnlyDictionary<string, object> customValues)
    {
        var set = new Dictionary<string, StateValue>();
        foreach (var reg in setting.Targets.OfType<RegTarget>())
        {
            var valueName = reg.ValueName ?? "KeyExists";
            if (customValues.TryGetValue(valueName, out var v))
                set[reg.Key] = v is null ? StateValue.Absent : StateValue.Of(v);
        }
        return Build(setting, new SettingState { Label = "__custom__", Set = set });
    }

    /// <summary>Turns "apply this Action" into write ops. An Action has no state - its setting-level Effects
    /// run on click. A RegistryWriteEffect becomes the same RegistryWriteOp a toggle's enabled value-write
    /// emits (via a synthesized single-path target, so the harness renders both sides identically); every other
    /// effect becomes an EffectOp. Order is the authored Effects order.</summary>
    public static IReadOnlyList<ApplyOp> BuildAction(Setting setting)
    {
        var ops = new List<ApplyOp>();
        foreach (var effect in setting.Effects)
        {
            if (effect is RegistryWriteEffect rw)
            {
                var target = new RegTarget(rw.ValueName, new[] { rw.Path }, rw.ValueName, rw.Kind)
                {
                    IsGroupPolicy = rw.IsGroupPolicy,
                };
                ops.Add(new RegistryWriteOp(target, rw.Path, rw.Value));
            }
            else
            {
                ops.Add(new EffectOp(effect));
            }
        }
        return ops;
    }

    /// <summary>Apply plan for a numeric (slider) setting: one PowerCfgSetOp per context value, the display value
    /// converted to system units (the inverse of the converter's system->display).</summary>
    public static IReadOnlyList<ApplyOp> BuildPowerCfgNumeric(Setting setting, IReadOnlyList<ContextValue> values)
    {
        var ops = new List<ApplyOp>();
        var pc = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault();
        if (pc is null) return ops;
        foreach (var cv in values)
            ops.Add(new PowerCfgSetOp(pc, cv.Context, ConvertToSystem(cv.Value, setting.Numeric?.Units)));
        return ops;
    }

    /// <summary>Apply plan for a separate-AC/DC powercfg SELECTION: writes the AC option's value to the AC context and
    /// the DC option's value to the DC context (asymmetric). Each option's value is that state's
    /// per-target Set payload - the same value Build(stateLabel) writes to BOTH contexts for the symmetric single-index
    /// path. An index whose state has no value for the target emits no op for that context (defensive; valid config/UI
    /// indices always resolve).</summary>
    public static IReadOnlyList<ApplyOp> BuildPowerCfgSelectionAcDc(Setting setting, int acIndex, int dcIndex)
    {
        var ops = new List<ApplyOp>();
        var pc = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault();
        if (pc is null) return ops;

        int? OptionValue(int idx) =>
            idx >= 0 && idx < setting.States.Count
                && setting.States[idx].Set.TryGetValue(pc.Key, out var sv) && sv.WritePayload is { } payload
                ? Convert.ToInt32(payload)
                : (int?)null;

        if (OptionValue(acIndex) is { } acValue) ops.Add(new PowerCfgSetOp(pc, PowerContext.AC, acValue));
        if (OptionValue(dcIndex) is { } dcValue) ops.Add(new PowerCfgSetOp(pc, PowerContext.DC, dcValue));
        return ops;
    }

    private static int ConvertToSystem(int displayValue, string? units) => units?.ToLowerInvariant() switch
    {
        "minutes" => displayValue * 60,
        "hours" => displayValue * 3600,
        _ => displayValue,   // milliseconds, percent, default 1:1
    };
}
