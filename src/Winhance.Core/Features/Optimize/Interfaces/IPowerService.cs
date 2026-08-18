using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Interfaces;

public interface IPowerService
{
    Task<PowerPlan?> GetActivePowerPlanAsync();
    Task<IEnumerable<object>> GetAvailablePowerPlansAsync();
    Task<bool> DeletePowerPlanAsync(string powerPlanGuid);

    // A ghost = a scheme carrying the Winhance GUID but the wrong name; switches to Balanced first if the ghost is
    // active. Run before the dropdown is populated so a corrupt plan is never shown. Never throws.
    Task CleanupCorruptWinhancePlanAsync();
}
