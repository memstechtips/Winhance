using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services;

public class PowerService(
    ILogService logService,
    IPowerSettingsQueryService powerSettingsQueryService,
    IPowerSchemeOperations powerSchemeOperations) : IPowerService, ISpecialSettingHandler
{

    /// <summary>
    /// Phase 6.7 Slice 8c: power-plan apply now runs through the catalog engine (the funnel routes it via
    /// ApplyRequestResolver -> PowerPlanActivateOp -> WindowsStateWriter.ActivatePowerPlan), so PowerService is no
    /// longer registered as an apply handler. This <see cref="ISpecialSettingHandler"/> entry point is a dead stub
    /// that always returns false; PowerService's live surface is now the corrupt-plan cleanup + the plan queries.
    /// </summary>
    public Task<bool> TryApplySpecialSettingAsync(string settingId, object value, bool additionalContext = false, ISettingApplicationService? settingApplicationService = null)
        => Task.FromResult(false);

    /// <summary>
    /// Detects and removes ghost/corrupt Winhance power plan entries that have the
    /// correct GUID but wrong name (e.g., "Unknown Power Plan"). These entries are
    /// visible to PowerEnumerate but are not functional plans. Called by the new-engine
    /// detection (SystemDetectionContext.PrefetchAsync) before the power-plan dropdown is
    /// populated, so a corrupt plan is never shown - the analog of the old discovery's
    /// "clean up before ComboBox setup". Never throws.
    /// </summary>
    public async Task CleanupCorruptWinhancePlanAsync()
    {
        try
        {
            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var winhanceGuid = "57696e68-616e-6365-506f-776572000000";

            var matchingPlan = systemPlans.FirstOrDefault(p =>
                string.Equals(p.Guid, winhanceGuid, StringComparison.OrdinalIgnoreCase));

            if (matchingPlan != null &&
                !string.Equals(matchingPlan.Name?.Trim(), "Winhance Power Plan", StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, $"[PowerService] Detected corrupt Winhance plan (name: '{matchingPlan.Name}'), cleaning up");

                // If the ghost is active, switch to Balanced first
                if (matchingPlan.IsActive)
                {
                    var balancedGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
                    var activateResult = powerSchemeOperations.SetActiveScheme(balancedGuid);
                    if (activateResult == PowerProf.ERROR_SUCCESS)
                    {
                        logService.Log(LogLevel.Info, "[PowerService] Switched to Balanced before deleting corrupt Winhance plan");
                    }
                }

                var deleteResult = powerSchemeOperations.DeleteScheme(Guid.Parse(winhanceGuid));
                if (deleteResult == PowerProf.ERROR_SUCCESS)
                {
                    logService.Log(LogLevel.Info, "[PowerService] Successfully deleted corrupt Winhance plan");
                    powerSettingsQueryService.InvalidateCache();
                }
                else
                {
                    logService.Log(LogLevel.Warning, $"[PowerService] Failed to delete corrupt Winhance plan: error 0x{deleteResult:X8}");
                }
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Error during Winhance plan cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the currently active power plan.
    /// </summary>
    /// <returns>
    /// The active <see cref="PowerPlan"/>, or <see langword="null"/> if the
    /// query fails (failure is logged as a warning, never thrown).
    /// </returns>
    public async Task<PowerPlan?> GetActivePowerPlanAsync()
    {
        try
        {
            return await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting active power plan: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets all power plans available on the system.
    /// </summary>
    /// <returns>
    /// A list of power plan objects, or an empty enumerable if the query
    /// fails (failure is logged as a warning, never thrown).
    /// </returns>
    public async Task<IEnumerable<object>> GetAvailablePowerPlansAsync()
    {
        try
        {
            var powerPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            return powerPlans.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting available power plans: {ex.Message}");
            return Enumerable.Empty<object>();
        }
    }

    /// <summary>
    /// Deletes a power plan by its GUID.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the plan was deleted;
    /// <see langword="false"/> if the plan is active, deletion failed, or an
    /// error occurred (all failures are logged, never thrown).
    /// </returns>
    public async Task<bool> DeletePowerPlanAsync(string powerPlanGuid)
    {
        try
        {
            logService.Log(LogLevel.Info, $"Attempting to delete power plan: {powerPlanGuid}");

            var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
            if (activePlan != null && string.Equals(activePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, "Cannot delete active power plan");
                return false;
            }

            var schemeGuid = Guid.Parse(powerPlanGuid);
            var result = powerSchemeOperations.DeleteScheme(schemeGuid);

            if (result == PowerProf.ERROR_SUCCESS)
            {
                powerSettingsQueryService.InvalidateCache();
                logService.Log(LogLevel.Info, $"Successfully deleted power plan: {powerPlanGuid}");
                return true;
            }
            else
            {
                logService.Log(LogLevel.Error, $"Failed to delete power plan: {powerPlanGuid}. Error code: {result}");
                return false;
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error deleting power plan: {ex.Message}");
            return false;
        }
    }

}
