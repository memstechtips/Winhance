using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Runs an apply plan against an <see cref="IStateWriter"/> best-effort: every op is attempted,
/// per-op failures are caught and collected, and an <see cref="ApplyResult"/> summarises the outcome.</summary>
public static class ApplyExecutor
{
    public static ApplyResult Execute(ApplyPlan plan, IStateWriter writer)
    {
        var failures = new List<string>();

        foreach (var op in plan.SyncOps)
        {
            try
            {
                bool ok = op switch
                {
                    RegistryWriteOp w => writer.WriteRegistry(w.Target, w.Path, w.Value),
                    RegistryDeleteOp d => writer.DeleteRegistry(d.Target, d.Path),
                    RegistryEnsureKeyOp e => writer.EnsureRegistryKey(e.Target, e.Path),
                    RegistryUnlockKeyOp u => writer.UnlockKey(u.Target, u.Path),
                    RegistryLockKeyOp l => writer.LockKey(l.Target, l.Path),
                    RegistryBitSetOp b => writer.SetRegistryBit(b.Target, b.Path, b.ByteIndex, b.BitMask, b.Set),
                    RegistryByteSetOp y => writer.SetRegistryByte(y.Target, y.Path, y.ByteIndex, y.Value),
                    RegistryStringFlagSetOp f => writer.SetRegistryStringFlag(f.Target, f.Path, f.FlagMask, f.AbsentBase, f.Set),
                    RegistryCompositeSetOp c => writer.SetRegistryComposite(c.Target, c.Path, c.CompositeKey, c.SubValue),
                    RegistryPerSubkeyWriteOp p => writer.WriteRegistryPerSubkey(p.Target, p.ParentPath, p.Value),
                    RegistryPerSubkeyDeleteOp p => writer.DeleteRegistryPerSubkey(p.Target, p.ParentPath),
                    TaskSetOp t => writer.SetTask(t.Target, t.Enabled),
                    PowerCfgSetOp p => writer.WritePowerCfgValue(p.Target, p.Context, p.Value),
                    EffectOp fx => writer.RunEffect(fx.Effect),
                    PowerPlanActivateOp pp => writer.ActivatePowerPlan(pp.Guid),
                    _ => true,
                };
                if (!ok)
                    failures.Add($"Op failed: {op}");
            }
            catch (Exception ex)
            {
                failures.Add($"Op threw: {op} — {ex.Message}");
            }
        }

        return new ApplyResult(plan.SyncOps.Count, failures.Count, failures);
    }
}
