using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Interface for power setting services.
    /// </summary>
    public interface IPowerSettingService
    {
        /// <summary>
        /// Gets all available power settings.
        /// </summary>
        /// <returns>A list of all power settings.</returns>
        List<PowerSettingDefinition> GetAllSettings();

        /// <summary>
        /// Gets all power setting subgroups.
        /// </summary>
        /// <returns>A list of all power setting subgroups.</returns>
        List<PowerSettingSubgroup> GetAllSubgroups();

        /// <summary>
        /// Gets a power setting by its GUID.
        /// </summary>
        /// <param name="guid">The GUID of the power setting.</param>
        /// <returns>The power setting definition, or null if not found.</returns>
        PowerSettingDefinition? GetSettingByGuid(string guid);

        /// <summary>
        /// Gets a power setting by its alias.
        /// </summary>
        /// <param name="alias">The alias of the power setting.</param>
        /// <returns>The power setting definition, or null if not found.</returns>
        PowerSettingDefinition? GetSettingByAlias(string alias);

        /// <summary>
        /// Gets the current value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <returns>A tuple containing the AC and DC values.</returns>
        Task<(int acValue, int dcValue)> GetSettingValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid);

        /// <summary>
        /// Gets the current AC value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <returns>The AC value.</returns>
        Task<int> GetAcValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid);

        /// <summary>
        /// Gets the current DC value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <returns>The DC value.</returns>
        Task<int> GetDcValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid);

        /// <summary>
        /// Sets the AC value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> SetAcValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid, int value);

        /// <summary>
        /// Sets the DC value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> SetDcValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid, int value);

        /// <summary>
        /// Applies a power setting value to a power plan.
        /// </summary>
        /// <param name="settingValue">The power setting value to apply.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> ApplySettingValueAsync(PowerSettingApplyValue settingValue);

        /// <summary>
        /// Checks if a power setting exists on the system.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID (optional). If null, only checks if the subgroup exists.</param>
        /// <returns>True if the setting exists, false otherwise.</returns>
        Task<bool> DoesSettingExistAsync(string powerPlanGuid, string subgroupGuid, string? settingGuid = null);
    }
}
