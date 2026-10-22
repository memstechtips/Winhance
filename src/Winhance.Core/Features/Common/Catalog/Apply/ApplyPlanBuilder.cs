using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Features.Common.Catalog;

public static class ApplyPlanBuilder
{
    public static IReadOnlyList<ApplyOp> Build(Setting setting, LocKey stateLabel, WinBuild? build = null, bool reset = false)
    {
        var state = setting.States.FirstOrDefault(s => s.Label == stateLabel)
            ?? throw new ArgumentException($"No state labelled '{stateLabel}' on setting '{setting.Id}'.", nameof(stateLabel));
        return Build(setting, state, build, reset);
    }

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
                    if (reg.ReadOnly)
                        break;
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
                        else if (reg.ListItem is { } listItem)
                        {
                            // Adds or removes one entry; the writer keeps every other entry in the list.
                            bool present = sv.WritePayload is { } lp && Convert.ToBoolean(lp);
                            ops.Add(new RegistryListItemSetOp(reg, path, reg.ListPrefix, listItem, present));
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
                                ops.Add(new RegistryEnsureKeyOp(reg, path));
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
                    // (the symmetric single-index semantics).
                    if (state.Set.TryGetValue(pc.Key, out var pv) && pv.WritePayload is { } powerPayload)
                    {
                        int value = Convert.ToInt32(powerPayload);
                        ops.Add(new PowerCfgSetOp(pc, PowerContext.AC, value));
                        ops.Add(new PowerCfgSetOp(pc, PowerContext.DC, value));
                    }
                    break;
            }
        }

        // Windows keeps each family's servers as one list, so all the DNS targets become one write.
        if (setting.Targets.OfType<DnsTarget>().Any(t => state.Set.ContainsKey(t.Key))
            && DnsServers(setting, key => state.Set.GetValueOrDefault(key)?.WritePayload) is { } servers)
            ops.Add(new DnsSetOp(servers));

        // Effects run after the registry/task state is in place.
        foreach (var effect in state.Effects)
            ops.Add(new EffectOp(effect));

        return ops;
    }

    // Synthesizes a transient state mapping each captured ValueName to a write (or a delete for a null value); registry
    // targets only - the resolver gates this to pure registry selections.
    public static IReadOnlyList<ApplyOp> BuildRegistryCustomState(Setting setting, IReadOnlyDictionary<string, object> customValues)
    {
        var set = new Dictionary<string, StateValue>();
        foreach (var reg in setting.Targets.OfType<RegTarget>())
        {
            var valueName = reg.ValueName ?? "KeyExists";
            if (customValues.TryGetValue(valueName, out var v))
                set[reg.Key] = v is null ? StateValue.Absent : StateValue.Of(v);
        }
        return Build(setting, new SettingState { Label = LocKey.Common.CustomState, Set = set });
    }

    // Null when no server is typed: an empty list would mean Automatic, and a mistyped value must not quietly hand
    // the machine back to DHCP.
    public static IReadOnlyList<ApplyOp>? BuildDnsCustomState(Setting setting, IReadOnlyDictionary<string, object> customValues) =>
        DnsServers(setting, customValues.GetValueOrDefault) is { Count: > 0 } servers ? [new DnsSetOp(servers)] : null;

    // Slot by slot in Windows' order. Null when a box holds something Windows would refuse: an address of the wrong
    // family, an alternate without a preferred, or a manual template that is not https.
    internal static IReadOnlyList<DnsServer>? DnsServers(Setting setting, Func<string, object?> valueOf)
    {
        var targets = setting.Targets.OfType<DnsTarget>().ToList();
        string? Text(DnsSlot slot, DnsPart part) =>
            targets.FirstOrDefault(t => t.Slot == slot && t.Part == part) is { } t
                && valueOf(t.Key)?.ToString()?.Trim() is { Length: > 0 } text ? text : null;

        var servers = new List<DnsServer>();
        foreach (var (preferred, alternate, ipv6) in new[] { (DnsSlot.Preferred, DnsSlot.Alternate, false), (DnsSlot.Preferred6, DnsSlot.Alternate6, true) })
        {
            if (Text(alternate, DnsPart.Address) is not null && Text(preferred, DnsPart.Address) is null)
                return null;

            foreach (var slot in new[] { preferred, alternate })
            {
                if (Text(slot, DnsPart.Address) is not { } address)
                    continue;
                if (!(ipv6 ? DnsAddress.IsIPv6(address) : DnsAddress.IsIPv4(address)))
                    return null;

                var doh = int.TryParse(Text(slot, DnsPart.Encryption), out var mode) && Enum.IsDefined((DohMode)mode) ? (DohMode)mode : DohMode.Off;
                var template = doh == DohMode.Manual ? Text(slot, DnsPart.Template) : null;
                if (doh == DohMode.Manual && (template is null || !DnsAddress.IsTemplate(template)))
                    return null;

                servers.Add(new DnsServer(address, doh, template, doh != DohMode.Off && bool.TryParse(Text(slot, DnsPart.Fallback), out var fb) && fb));
            }
        }
        return servers;
    }

    // A Custom state on a setting with NO registry target to write: the setting's CustomStateScripts ARE the apply.
    public static IReadOnlyList<ApplyOp> BuildCustomStateScripts(Setting setting) =>
        setting.CustomStateScripts.Select(effect => (ApplyOp)new EffectOp(effect)).ToList();

    // A RegistryWriteEffect becomes the same RegistryWriteOp a toggle's value write emits (via a synthesized single-path
    // target) so the harness renders both sides identically; every other effect becomes an EffectOp, in authored order.
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

    public static IReadOnlyList<ApplyOp> BuildPowerCfgNumeric(Setting setting, IReadOnlyList<ContextValue> values)
    {
        var ops = new List<ApplyOp>();
        var pc = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault();
        if (pc is null) return ops;
        foreach (var cv in values)
            ops.Add(new PowerCfgSetOp(pc, cv.Context, ConvertToSystem(cv.Value, setting.Numeric?.Units)));
        return ops;
    }

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
