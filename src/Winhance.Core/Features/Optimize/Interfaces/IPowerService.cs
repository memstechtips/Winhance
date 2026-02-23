using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Interfaces
{
    public interface IPowerService : IDomainService
    {
        Task<PowerPlan?> GetActivePowerPlanAsync();
        Task<IEnumerable<object>> GetAvailablePowerPlansAsync();
        Task<bool> DeletePowerPlanAsync(string powerPlanGuid);
    }
}
