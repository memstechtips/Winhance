using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Services
{
    /// <summary>
    /// Interface for power plan management services.
    /// </summary>
    public interface IPowerPlanManagerService
    {
        /// <summary>
        /// Gets all available power plans.
        /// </summary>
        /// <returns>A list of power plans.</returns>
        Task<List<PowerPlan>> GetPowerPlansAsync();

        /// <summary>
        /// Gets the active power plan.
        /// </summary>
        /// <returns>The active power plan, or null if not found.</returns>
        Task<PowerPlan?> GetActivePowerPlanAsync();

        /// <summary>
        /// Gets the active power plan GUID.
        /// </summary>
        /// <returns>The active power plan GUID, or null if not found.</returns>
        Task<string> GetActivePowerPlanGuidAsync();

        /// <summary>
        /// Sets the active power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID to set as active.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> SetActivePowerPlanAsync(string powerPlanGuid);

        /// <summary>
        /// Creates a new power plan.
        /// </summary>
        /// <param name="name">The name of the new power plan.</param>
        /// <param name="sourceGuid">The source power plan GUID to duplicate from.</param>
        /// <returns>The GUID of the new power plan, or null if creation failed.</returns>
        Task<string?> CreatePowerPlanAsync(string name, string sourceGuid);

        /// <summary>
        /// Deletes a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID to delete.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> DeletePowerPlanAsync(string powerPlanGuid);
    }
}
