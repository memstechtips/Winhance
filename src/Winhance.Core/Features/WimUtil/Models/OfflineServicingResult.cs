namespace Winhance.Core.Features.WimUtil.Models;

public enum ServicedItemOutcome
{
    NotStaged,
    Added,
    FellBackToSetup
}

public sealed record OfflineServicingResult(ServicedItemOutcome Drivers, ServicedItemOutcome Update)
{
    public IReadOnlyList<ServicingPhaseUpdate> Phases { get; init; } = [];

    public bool AnyFellBack => Drivers == ServicedItemOutcome.FellBackToSetup || Update == ServicedItemOutcome.FellBackToSetup;
}
