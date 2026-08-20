namespace Winhance.Core.Features.Common.Catalog;

// Partitioned by what the synchronous writer can carry out: ApplyExecutor only ever sees SyncOps, so an effect that
// launches a process cannot reach IStateWriter; the caller awaits AsyncEffects separately.
public sealed record ApplyPlan(IReadOnlyList<ApplyOp> SyncOps, IReadOnlyList<Effect> AsyncEffects)
{
    // Both halves: the denominator a caller should report.
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
