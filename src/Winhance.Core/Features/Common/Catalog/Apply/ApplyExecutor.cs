using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Runs an apply plan against an <see cref="IStateWriter"/> best-effort: every op is attempted,
/// per-op failures are caught and collected, and an <see cref="ApplyResult"/> summarises the outcome.</summary>
public static class ApplyExecutor
{
    public static ApplyResult Execute(IReadOnlyList<ApplyOp> plan, IStateWriter writer)
    {
        var failures = new List<string>();

        foreach (var op in plan)
        {
            try
            {
                bool ok = op switch
                {
                    RegistryWriteOp w => writer.WriteRegistry(w.Target, w.Path, w.Value),
                    RegistryDeleteOp d => writer.DeleteRegistry(d.Target, d.Path),
                    RegistryEnsureKeyOp e => writer.EnsureRegistryKey(e.Target, e.Path),
                    TaskSetOp t => writer.SetTask(t.Target, t.Enabled),
                    EffectOp fx => writer.RunEffect(fx.Effect),
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

        return new ApplyResult(plan.Count, failures.Count, failures);
    }
}
