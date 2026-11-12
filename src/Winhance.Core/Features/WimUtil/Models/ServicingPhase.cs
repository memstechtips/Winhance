namespace Winhance.Core.Features.WimUtil.Models;

// Declared in the order the build runs them: the cumulative update goes in and the store is cleaned before .NET 3.5
// is added, which is Microsoft's media servicing order.
public enum ServicingPhase
{
    PrepareImage,
    Mount,
    Update,
    Drivers,
    RemoveApps,
    ApplySettings,
    Cleanup,
    DotNet,
    Save,
    Export,
    RemoveDotNetSource
}

public enum ServicingPhaseState
{
    Pending,
    Running,
    Done,
    Skipped,
    Failed
}

public sealed record ServicingPhaseUpdate(ServicingPhase Phase, ServicingPhaseState State);
