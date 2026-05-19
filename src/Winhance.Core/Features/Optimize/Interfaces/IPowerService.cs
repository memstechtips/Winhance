using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Interfaces;

public interface IPowerService
{
    Task<PowerPlan?> GetActivePowerPlanAsync();
    Task<IEnumerable<object>> GetAvailablePowerPlansAsync();
    Task<bool> DeletePowerPlanAsync(string powerPlanGuid);
}
