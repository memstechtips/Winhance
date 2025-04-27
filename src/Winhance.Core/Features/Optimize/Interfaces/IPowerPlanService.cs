using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Interfaces
{
    /// <summary>
    /// Provides functionality for managing Windows power plans.
    /// </summary>
    public interface IPowerPlanService
    {
        /// <summary>
        /// Gets the GUID of the currently active power plan.
        /// </summary>
        /// <returns>The GUID of the active power plan.</returns>
        Task<string> GetActivePowerPlanGuidAsync();

        /// <summary>
        /// Sets the active power plan.
        /// </summary>
        /// <param name="planGuid">The GUID of the power plan to set as active.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> SetPowerPlanAsync(string planGuid);

        /// <summary>
        /// Ensures that a power plan with the specified GUID exists.
        /// If it doesn't exist, creates it from the source plan if specified.
        /// </summary>
        /// <param name="planGuid">The GUID of the power plan to ensure exists.</param>
        /// <param name="sourcePlanGuid">The GUID of the source plan to create from, if needed.</param>
        /// <returns>True if the plan exists or was created successfully; otherwise, false.</returns>
        Task<bool> EnsurePowerPlanExistsAsync(string planGuid, string sourcePlanGuid = null);

        /// <summary>
        /// Gets a list of all available power plans.
        /// </summary>
        /// <returns>A list of available power plans.</returns>
        Task<List<PowerPlan>> GetAvailablePowerPlansAsync();
        
        /// <summary>
        /// Executes a PowerCfg command.
        /// </summary>
        /// <param name="command">The PowerCfg command to execute.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> ExecutePowerCfgCommandAsync(string command);
        
        /// <summary>
        /// Applies a power setting using PowerCfg.
        /// </summary>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="isAcSetting">True if this is an AC (plugged in) setting; false for DC (battery) setting.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> ApplyPowerSettingAsync(string subgroupGuid, string settingGuid, string value, bool isAcSetting);
        
        /// <summary>
        /// Applies a collection of PowerCfg settings.
        /// </summary>
        /// <param name="settings">The collection of PowerCfg settings to apply.</param>
        /// <returns>True if all operations succeeded; otherwise, false.</returns>
        Task<bool> ApplyPowerCfgSettingsAsync(List<PowerCfgSetting> settings);
        
        /// <summary>
        /// Checks if a PowerCfg setting is currently applied.
        /// </summary>
        /// <param name="setting">The PowerCfg setting to check.</param>
        /// <returns>True if the setting is applied; otherwise, false.</returns>
        Task<bool> IsPowerCfgSettingAppliedAsync(PowerCfgSetting setting);
        
        /// <summary>
        /// Checks if all PowerCfg settings in a collection are currently applied.
        /// </summary>
        /// <param name="settings">The collection of PowerCfg settings to check.</param>
        /// <returns>True if all settings are applied; otherwise, false.</returns>
        Task<bool> AreAllPowerCfgSettingsAppliedAsync(List<PowerCfgSetting> settings);
    }
}