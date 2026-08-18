using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Interfaces;

// A leaf of IStateWriter (no PowerService or IStateWriter back-reference), so wiring it into WindowsStateWriter
// is DI-cycle-safe. The post-activation side-effects (PowerPlanChangedEvent, the recommended re-apply)
// deliberately stay with the caller.
public interface IPowerPlanActivationService
{
    // The FINAL activated GUID can differ from the requested one when a plan is imported/duplicated under a new
    // GUID; the caller's post-activation tail keys off it.
    Task<(bool Success, string ActivatedGuid)> EnsureActivatedAsync(string powerPlanGuid, string? planName = null);

    Task<PowerPlanImportResult> ImportPowerPlanAsync(PredefinedPowerPlan predefinedPlan);

    // Skips when already active; invalidates the power-settings query cache on success.
    Task<bool> SetActivePowerPlanAsync(string powerPlanGuid);
}
