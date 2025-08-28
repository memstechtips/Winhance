using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Interfaces
{
    /// <summary>
    /// Service interface for managing power management optimization settings.
    /// Handles power plans, sleep settings, and power-related optimizations.
    /// </summary>
    public interface IPowerService : IDomainService
    {
        /// <summary>
        /// Gets the currently active power plan.
        /// </summary>
        /// <returns>The active power plan, or null if none found.</returns>
        Task<PowerPlan?> GetActivePowerPlanAsync();

        /// <summary>
        /// Applies an advanced power setting value.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <param name="acValue">The AC power value.</param>
        /// <param name="dcValue">The DC power value.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplyAdvancedPowerSettingAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string settingGuid,
            int acValue,
            int dcValue
        );

        /// <summary>
        /// Checks system capabilities for power management (battery, lid detection).
        /// </summary>
        /// <returns>Dictionary with capability information.</returns>
        Task<Dictionary<string, bool>> CheckPowerSystemCapabilitiesAsync();
        
        /// <summary>
        /// Gets available power plans asynchronously.
        /// </summary>
        /// <returns>A collection of available power plans.</returns>
        Task<IEnumerable<object>> GetAvailablePowerPlansAsync();
        
        /// <summary>
        /// Gets advanced power setting groups asynchronously.
        /// </summary>
        /// <returns>A collection of advanced power setting groups.</returns>
        Task<IEnumerable<AdvancedPowerSettingGroup>> GetAdvancedPowerSettingGroupsAsync();

        /// <summary>
        /// Sets the active power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The GUID of the power plan to activate.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> SetActivePowerPlanAsync(string powerPlanGuid);

        /// <summary>
        /// Gets the current value of a power setting.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <returns>A tuple containing the AC and DC values.</returns>
        Task<(int acValue, int dcValue)> GetSettingValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid);

        /// <summary>
        /// Applies a dynamic power setting change.
        /// Handles all business logic for extracting metadata and applying the setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to apply.</param>
        /// <param name="isSelected">For toggle controls, whether the setting is selected.</param>
        /// <param name="selectedValue">For value controls, the selected value.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplyDynamicPowerSettingAsync(string settingId, bool isSelected, object? selectedValue);

        /// <summary>
        /// Gets the available timeout options for display and sleep settings.
        /// Domain service provides business data, not hardcoded in ViewModel.
        /// </summary>
        /// <returns>Collection of timeout option display strings.</returns>
        IEnumerable<string> GetTimeoutOptions();
    }
}
