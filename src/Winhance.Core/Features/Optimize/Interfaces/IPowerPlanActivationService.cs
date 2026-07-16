using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Interfaces;

/// <summary>
/// Owns the power-plan activation orchestration extracted from PowerService:
/// ensure a plan is installed (importing a predefined-but-not-installed plan), activate it, and keep
/// the power-settings query cache used while populating a freshly-imported plan. Async-native and a
/// leaf of IStateWriter (it holds no PowerService and no IStateWriter back-reference, so wiring it into
/// WindowsStateWriter is DI-cycle-safe). The post-activation side-effects (the PowerPlanChangedEvent
/// publish and the recommended re-apply) deliberately stay with the caller, not this service.
/// </summary>
public interface IPowerPlanActivationService
{
    /// <summary>
    /// Ensures the plan identified by <paramref name="powerPlanGuid"/> is installed and active, importing a
    /// predefined-but-not-installed plan first. Returns success plus the FINAL activated GUID, which can differ
    /// from the requested GUID when a plan is imported/duplicated under a new GUID, so the caller's
    /// post-activation tail keys off the actually-activated GUID.
    /// </summary>
    Task<(bool Success, string ActivatedGuid)> EnsureActivatedAsync(string powerPlanGuid, string? planName = null);

    /// <summary>
    /// Imports a predefined power plan onto the system (duplication, with a backup/restore fallback).
    /// </summary>
    Task<PowerPlanImportResult> ImportPowerPlanAsync(PredefinedPowerPlan predefinedPlan);

    /// <summary>
    /// Activates an already-installed scheme by GUID (skips when it is already active) and invalidates the
    /// power-settings query cache on success.
    /// </summary>
    Task<bool> SetActivePowerPlanAsync(string powerPlanGuid);

    /// <summary>
    /// Clears the power-settings query cache.
    /// </summary>
    void InvalidateSettingsCache();
}
