using Windows.Win32.Foundation;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Optimize.Services;

internal class PowerService(
    ILogService logService,
    IPowerSettingsQueryService powerSettingsQueryService,
    IPowerSchemeOperations powerSchemeOperations) : IPowerService, ISpecialSettingHandler
{

    // Dead stub, always false: power-plan apply runs through the catalog engine (ApplyRequestResolver ->
    // PowerPlanActivateOp -> WindowsStateWriter), so PowerService is not an apply handler.
    public Task<bool> TryApplySpecialSettingAsync(string settingId, object value, bool additionalContext = false, ISettingApplicationService? settingApplicationService = null)
        => Task.FromResult(false);

    // Ghost entries (right GUID, wrong name, e.g. "Unknown Power Plan") are visible to PowerEnumerate but not
    // functional. Called from SystemDetectionContext.PrefetchAsync before the dropdown is populated. Never throws.
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

                if (matchingPlan.IsActive)
                {
                    var balancedGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
                    var activateResult = powerSchemeOperations.SetActiveScheme(balancedGuid);
                    if (activateResult == (uint)WIN32_ERROR.ERROR_SUCCESS)
                    {
                        logService.Log(LogLevel.Info, "[PowerService] Switched to Balanced before deleting corrupt Winhance plan");
                    }
                }

                var deleteResult = powerSchemeOperations.DeleteScheme(Guid.Parse(winhanceGuid));
                if (deleteResult == (uint)WIN32_ERROR.ERROR_SUCCESS)
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

            if (result == (uint)WIN32_ERROR.ERROR_SUCCESS)
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
