using System;
using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>An apply plan partitioned by what the synchronous writer can actually carry out.
/// <see cref="ApplyExecutor"/> only ever sees <see cref="SyncOps"/>, so an effect that launches a process
/// cannot reach <see cref="IStateWriter"/>; the caller awaits <see cref="AsyncEffects"/> separately.</summary>
public sealed record ApplyPlan(IReadOnlyList<ApplyOp> SyncOps, IReadOnlyList<Effect> AsyncEffects)
{
    /// <summary>Operations in the whole plan, both halves - what a caller should report as the denominator.</summary>
    public int Total => SyncOps.Count + AsyncEffects.Count;

    public static ApplyPlan From(IReadOnlyList<ApplyOp> ops)
    {
        // Most plans have no async effects, so avoid copying the list in that case.
        if (!ops.Any(o => o is EffectOp { Effect.IsAsyncIo: true }))
            return new ApplyPlan(ops, Array.Empty<Effect>());

        var sync = new List<ApplyOp>(ops.Count);
        var deferred = new List<Effect>();
        foreach (var op in ops)
        {
            if (op is EffectOp fx && fx.Effect.IsAsyncIo)
                deferred.Add(fx.Effect);
            else
                sync.Add(op);
        }

        return new ApplyPlan(sync, deferred);
    }
}
