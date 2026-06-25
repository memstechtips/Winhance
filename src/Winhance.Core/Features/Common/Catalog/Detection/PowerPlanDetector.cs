namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Detects the active power plan. Its options are runtime (installed plans), so it returns the active power
/// scheme GUID directly rather than matching a static state.</summary>
public sealed class PowerPlanDetector : IStateDetector
{
    public string? Detect(Setting setting, IDetectionContext context) => context.ActivePowerPlanGuid();
}
