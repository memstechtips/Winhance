namespace Winhance.Core.Features.Common.Catalog;

public sealed class PowerPlanDetector : IStateDetector
{
    public string? Detect(Setting setting, IDetectionContext context) => context.ActivePowerPlanGuid();
}
