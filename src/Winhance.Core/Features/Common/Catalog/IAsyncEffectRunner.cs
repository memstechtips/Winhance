namespace Winhance.Core.Features.Common.Catalog;

// Process-launching effects stay off IStateWriter, which is synchronous because almost everything it does is a blocking OS call.
public interface IAsyncEffectRunner
{
    // False also for an effect kind it does not recognise, so a new kind cannot silently become a no-op.
    Task<bool> RunAsync(Effect effect);

    // Sequential: these mutate machine state and a plan may order them meaningfully.
    Task<IReadOnlyList<string>> RunAllAsync(IReadOnlyList<Effect> effects);
}
