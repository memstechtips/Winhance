using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Interfaces;

/// <summary>
/// Owns the power-plan activation orchestration extracted from PowerService (Phase 6.7 Slice 8b-1):
/// ensure a plan is installed (importing a predefined-but-not-installed plan), activate it, and keep
/// the power SettingDefinitions cache used while populating a freshly-imported plan. Async-native and a
/// leaf of IStateWriter (it holds no PowerService and no IStateWriter back-reference, so wiring it into
/// WindowsStateWriter is DI-cycle-safe). The side-effects the old ApplyPowerPlanByGuidAsync ran AFTER a
/// successful activation (the PowerPlanChangedEvent publish and the recommended re-apply) deliberately
/// stay with the caller and move to their principled homes in Slice 8b-2 (D1/D2).
/// </summary>
public interface IPowerPlanActivationService
{
    /// <summary>
    /// Ensures the plan identified by <paramref name="powerPlanGuid"/> is installed and active, importing a
    /// predefined-but-not-installed plan first. Returns success plus the FINAL activated GUID, which can differ
    /// from the requested GUID when a plan is imported/duplicated under a new GUID, so the caller's
    /// post-activation tail keys off the same GUID the old monolithic method did.
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
    /// Clears the cached power SettingDefinitions (the old PowerService private InvalidateCache).
    /// </summary>
    void InvalidateSettingsCache();
}
