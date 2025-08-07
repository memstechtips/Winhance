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
    }
}
