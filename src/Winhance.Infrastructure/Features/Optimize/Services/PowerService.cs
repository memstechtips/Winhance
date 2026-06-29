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
using Winhance.Core.Features.Common.Events;
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
    IEventBus eventBus,
    IPowerPlanComboBoxService powerPlanComboBoxService,
    IPowerSchemeOperations powerSchemeOperations,
    IConfigImportState configImportState,
    IPowerPlanActivationService activation) : IPowerService, ISpecialSettingHandler
{

    /// <summary>
    /// Attempts to apply a special (non-registry) setting. For power-plan-selection,
    /// delegates to plan import/activation logic.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the setting was handled and applied successfully;
    /// <see langword="false"/> if the setting is not a special setting, the value type
    /// is unsupported, or the operation failed.
    /// Never throws for expected business failures; errors are logged internally.
    /// </returns>
    public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false, ISettingApplicationService? settingApplicationService = null)
    {
        if (setting.Id == SettingIds.PowerPlanSelection)
        {
            logService.Log(LogLevel.Info, "[PowerService] Applying power-plan-selection");

            if (value is Dictionary<string, object> planDict)
            {
                var guid = planDict["Guid"].ToString()!;
                var name = planDict["Name"].ToString()!;

                logService.Log(LogLevel.Info, $"[PowerService] Config import: applying power plan {name} ({guid})");
                return await ApplyPowerPlanByGuidAsync(setting, guid, name, settingApplicationService).ConfigureAwait(false);
            }

            // New-model UI selection (Phase 6.7 Slice 7b): the stored value is the scheme GUID directly (no index
            // round-trip). ApplyPowerPlanByGuidAsync drives everything off the GUID - applying it, or importing the
            // predefined plan when not installed (matched by GUID against BuiltInPowerPlans); the name is logging-only.
            if (value is string planGuid)
            {
                logService.Log(LogLevel.Info, $"[PowerService] UI selection: applying power plan by GUID {planGuid}");
                return await ApplyPowerPlanByGuidAsync(setting, planGuid, planGuid, settingApplicationService).ConfigureAwait(false);
            }

            if (value is int index)
            {
                logService.Log(LogLevel.Info, $"[PowerService] UI selection: applying power plan at index {index}");

                var resolution = await powerPlanComboBoxService.ResolvePowerPlanByIndexAsync(index).ConfigureAwait(false);
                if (!resolution.Success)
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to resolve power plan index: {resolution.ErrorMessage}");
                    return false;
                }

                return await ApplyPowerPlanSelectionAsync(setting, resolution.Guid, index, resolution.DisplayName, settingApplicationService).ConfigureAwait(false);
            }

            logService.Log(LogLevel.Error, $"[PowerService] Invalid power plan value type: {value?.GetType().Name}");
            return false;
        }

        return false;
    }

    public async Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(IEnumerable<SettingDefinition> settings)
    {
        var results = new Dictionary<string, Dictionary<string, object?>>();

        var powerPlanSetting = settings.FirstOrDefault(s => s.Id == SettingIds.PowerPlanSelection);
        if (powerPlanSetting != null)
        {
            // Check for ghost/corrupt Winhance plan and clean up before ComboBox setup
            await CleanupCorruptWinhancePlanAsync().ConfigureAwait(false);

            var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
            var rawValues = new Dictionary<string, object?>
            {
                ["ActivePowerPlan"] = activePlan?.Name,
                ["ActivePowerPlanGuid"] = activePlan?.Guid
            };
            results[SettingIds.PowerPlanSelection] = rawValues;
        }

        return results;
    }

    /// <summary>
    /// Detects and removes ghost/corrupt Winhance power plan entries that have the
    /// correct GUID but wrong name (e.g., "Unknown Power Plan"). These entries are
    /// visible to PowerEnumerate but are not functional plans.
    /// </summary>
    private async Task CleanupCorruptWinhancePlanAsync()
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

    /// <summary>
    /// Applies a power plan selected via the UI combo box.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the plan was activated successfully;
    /// <see langword="false"/> if the plan could not be imported, found, or activated.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="powerPlanGuid"/> is null or empty (programmer error).
    /// </exception>
    private async Task<bool> ApplyPowerPlanSelectionAsync(SettingDefinition setting, string powerPlanGuid, int planIndex, string planName, ISettingApplicationService? settingApplicationService)
    {
        logService.Log(LogLevel.Info, $"[PowerService] Applying power plan: {planName} ({powerPlanGuid})");

        if (string.IsNullOrEmpty(powerPlanGuid))
        {
            throw new ArgumentException("Power plan GUID cannot be null or empty");
        }

        var previousPlan = await GetActivePowerPlanAsync().ConfigureAwait(false);

        var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var existingSystemPlan = systemPlans.FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));
        var planExists = existingSystemPlan != null;

        // Detect corrupt/ghost Winhance plan: GUID matches but name is wrong (e.g., "Unknown Power Plan")
        if (planExists && IsWinhancePowerPlan(powerPlanGuid) &&
            !string.Equals(existingSystemPlan!.Name?.Trim(), "Winhance Power Plan", StringComparison.OrdinalIgnoreCase))
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Found corrupt Winhance plan (name: '{existingSystemPlan.Name}'), deleting and recreating");
            var corruptGuid = Guid.Parse(powerPlanGuid);
            powerSchemeOperations.DeleteScheme(corruptGuid);
            powerSettingsQueryService.InvalidateCache();
            planExists = false;
        }

        bool success = false;

        if (!planExists)
        {
            var predefinedPlan = PowerPlanDefinitions.BuiltInPowerPlans
                .FirstOrDefault(p => string.Equals(p.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase));

            if (predefinedPlan != null)
            {
                logService.Log(LogLevel.Info, $"[PowerService] Plan '{predefinedPlan.Name}' not found, attempting import");
                var importResult = await activation.ImportPowerPlanAsync(predefinedPlan).ConfigureAwait(false);

                if (importResult.Success)
                {
                    logService.Log(LogLevel.Info, $"[PowerService] Successfully imported '{predefinedPlan.Name}', activating");
                    await Task.Delay(200).ConfigureAwait(false);

                    var importedSchemeGuid = Guid.Parse(importResult.ImportedGuid);
                    var activateResult = powerSchemeOperations.SetActiveScheme(importedSchemeGuid);
                    success = activateResult == PowerProf.ERROR_SUCCESS;

                    if (success)
                    {
                        powerSettingsQueryService.InvalidateCache();
                        activation.InvalidateSettingsCache();
                        logService.Log(LogLevel.Info, $"[PowerService] Successfully activated imported plan");
                    }
                    else
                    {
                        logService.Log(LogLevel.Warning, $"[PowerService] First activation failed, retrying...");
                        await Task.Delay(500).ConfigureAwait(false);
                        activateResult = powerSchemeOperations.SetActiveScheme(importedSchemeGuid);
                        success = activateResult == PowerProf.ERROR_SUCCESS;

                        if (success)
                        {
                            powerSettingsQueryService.InvalidateCache();
                            activation.InvalidateSettingsCache();
                            logService.Log(LogLevel.Info, $"[PowerService] Successfully activated on retry");
                        }
                        else
                        {
                            logService.Log(LogLevel.Error, $"[PowerService] Failed to activate after import. Error code: {activateResult}");
                        }
                    }

                    powerPlanGuid = importResult.ImportedGuid;
                }
                else
                {
                    logService.Log(LogLevel.Error, $"[PowerService] Failed to import plan: {importResult.ErrorMessage}");
                    return false;
                }
            }
            else
            {
                logService.Log(LogLevel.Error, $"[PowerService] Unknown power plan GUID: {powerPlanGuid}");
                return false;
            }
        }
        else
        {
            success = await activation.SetActivePowerPlanAsync(powerPlanGuid).ConfigureAwait(false);
        }

        if (success)
        {
            logService.Log(LogLevel.Info, $"[PowerService] Publishing PowerPlanChangedEvent");

            eventBus.Publish(new PowerPlanChangedEvent
            {
                PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                NewPlanGuid = powerPlanGuid,
                NewPlanName = planName,
                NewPlanIndex = planIndex
            });

            if (IsWinhancePowerPlan(powerPlanGuid))
            {
                if (configImportState.IsActive && configImportState.ImportSuppliesPowerValues)
                {
                    logService.Log(LogLevel.Info,
                        "[PowerService] Skipping recommended power re-apply: active config import supplies individual power values");
                }
                else
                {
                    await ApplyWinhanceRecommendedSettingsAsync(settingApplicationService).ConfigureAwait(false);
                }
            }

            logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan");
        }

        return success;
    }

    /// <summary>
    /// Applies a power plan identified by its GUID (used during config import).
    /// Creates the plan by duplicating Balanced if not found as a predefined or existing plan.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the plan was activated successfully;
    /// <see langword="false"/> if the plan could not be imported, created, or activated.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="powerPlanGuid"/> is null or empty (programmer error).
    /// </exception>
    private async Task<bool> ApplyPowerPlanByGuidAsync(SettingDefinition setting, string powerPlanGuid, string planName, ISettingApplicationService? settingApplicationService)
    {
        var previousPlan = await GetActivePowerPlanAsync().ConfigureAwait(false);

        var (success, activatedGuid) = await activation.EnsureActivatedAsync(powerPlanGuid, planName).ConfigureAwait(false);

        if (success)
        {
            var options = await powerPlanComboBoxService.GetPowerPlanOptionsAsync().ConfigureAwait(false);
            var planIndex = options.FindIndex(o =>
                string.Equals(o.SystemPlan?.Guid, activatedGuid, StringComparison.OrdinalIgnoreCase));

            eventBus.Publish(new PowerPlanChangedEvent
            {
                PreviousPlanGuid = previousPlan?.Guid ?? string.Empty,
                NewPlanGuid = activatedGuid,
                NewPlanName = planName,
                NewPlanIndex = planIndex >= 0 ? planIndex : 0
            });

            if (IsWinhancePowerPlan(activatedGuid))
            {
                if (configImportState.IsActive && configImportState.ImportSuppliesPowerValues)
                {
                    logService.Log(LogLevel.Info,
                        "[PowerService] Skipping recommended power re-apply: active config import supplies individual power values");
                }
                else
                {
                    await ApplyWinhanceRecommendedSettingsAsync(settingApplicationService).ConfigureAwait(false);
                }
            }

            logService.Log(LogLevel.Info, $"[PowerService] Successfully applied power plan '{planName}'");
        }

        return success;
    }

    private static bool IsWinhancePowerPlan(string guid) =>
        IsWinhancePowerPlan(guid, null);

    private static bool IsWinhancePowerPlan(string guid, string? name) =>
        string.Equals(guid, "57696e68-616e-6365-506f-776572000000", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name?.Trim(), "Winhance Power Plan", StringComparison.OrdinalIgnoreCase);

    private async Task ApplyWinhanceRecommendedSettingsAsync(ISettingApplicationService? settingApplicationService)
    {
        try
        {
            if (settingApplicationService == null)
                throw new InvalidOperationException("settingApplicationService is required for applying recommended settings");
            logService.Log(LogLevel.Info, "[PowerService] Applying recommended settings for Winhance Power Plan");
            await settingApplicationService.ApplyRecommendedSettingsForFeatureAsync(SettingIds.PowerPlanSelection).ConfigureAwait(false);
            logService.Log(LogLevel.Info, "[PowerService] Successfully applied recommended settings for Winhance Power Plan");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerService] Failed to apply recommended settings: {ex.Message}");
        }
    }

}
