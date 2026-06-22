using System;
using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Turns "apply state &lt;label&gt; of &lt;setting&gt;" into an ordered list of declarative write ops - the
/// forward direction of target-by-state. Pure; no I/O. Registry/task/effects are handled here; powercfg
/// apply is handled by the dedicated power work and throws here if encountered.
/// </summary>
public static class ApplyPlanBuilder
{
    public static IReadOnlyList<ApplyOp> Build(Setting setting, string stateLabel, WinBuild? build = null)
    {
        var state = setting.States.FirstOrDefault(s => s.Label == stateLabel)
            ?? throw new ArgumentException($"No state labelled '{stateLabel}' on setting '{setting.Id}'.", nameof(stateLabel));

        var ops = new List<ApplyOp>();

        // A setting that applies via a .reg import does NOT write its registry targets - those are detect-only;
        // the import is the apply. Mirrors the old apply, which skips registry writes when RegContents is present.
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
                    if (!state.Set.TryGetValue(reg.Key, out var sv))
                        continue; // state doesn't cover this target (e.g. a fallback's partial Set) - leave it alone
                    foreach (var path in reg.Paths)
                    {
                        if (reg.PerNetworkInterface || reg.PerMonitor)
                        {
                            // Expand-and-write-each: the old apply enumerates the parent key's sub-keys and applies
                            // the same write to each. Enumeration is deferred to the writer; emit the per-sub-key intent.
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
                        else if (sv.DeleteOnWrite)
                            ops.Add(new RegistryDeleteOp(reg, path));
                        else if (sv.WritePayload is { } payload)
                            ops.Add(new RegistryWriteOp(reg, path, payload));
                        else if (sv.AcceptsAnyPresent)
                            ops.Add(new RegistryEnsureKeyOp(reg, path)); // Exists: ensure key/value present
                        // else: nothing concrete to write (defensive; the validator should prevent this)
                    }
                    break;

                case TaskTarget task:
                    if (state.Set.TryGetValue(task.Key, out var tv) && tv.WritePayload is { } tval)
                        ops.Add(new TaskSetOp(task, Convert.ToBoolean(tval)));
                    break;

                case PowerCfgTarget:
                    throw new NotSupportedException(
                        $"PowerCfgTarget apply for setting '{setting.Id}' is handled by the dedicated power work, not the generic planner.");
            }
        }

        // Effects run after the registry/task state is in place.
        foreach (var effect in state.Effects)
            ops.Add(new EffectOp(effect));

        return ops;
    }
}
