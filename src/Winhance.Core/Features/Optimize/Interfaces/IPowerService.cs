using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Interfaces;

public interface IPowerService
{
    Task<PowerPlan?> GetActivePowerPlanAsync();
    Task<IEnumerable<object>> GetAvailablePowerPlansAsync();
    Task<bool> DeletePowerPlanAsync(string powerPlanGuid);

    /// <summary>Removes a corrupt/ghost "Winhance Power Plan" (a scheme carrying the Winhance GUID but the wrong
    /// name) - switching to Balanced first if the ghost is active. A no-op when no corrupt plan exists. Run before
    /// the power-plan dropdown is populated so a corrupt plan is never shown. Never throws.</summary>
    Task CleanupCorruptWinhancePlanAsync();
}
