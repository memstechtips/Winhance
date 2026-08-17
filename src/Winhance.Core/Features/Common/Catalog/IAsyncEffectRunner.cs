using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Runs the apply effects that launch a process and wait for it (a PowerShell script, a .reg
/// import). These stay off <see cref="IStateWriter"/>, which is synchronous because almost everything it
/// does is a blocking OS call; <see cref="ApplyPlan"/> routes them here instead.</summary>
public interface IAsyncEffectRunner
{
    /// <summary>Runs one effect. False on failure, including an effect kind it does not recognise - so a
    /// new kind cannot silently become a no-op.</summary>
    Task<bool> RunAsync(Effect effect);

    /// <summary>Runs effects in order, returning the failure messages. Sequential because these mutate
    /// machine state and a plan may order them meaningfully.</summary>
    Task<IReadOnlyList<string>> RunAllAsync(IReadOnlyList<Effect> effects);
}
