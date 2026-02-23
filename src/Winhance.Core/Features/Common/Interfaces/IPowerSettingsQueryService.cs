using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerSettingsQueryService
{
    Task<List<PowerPlan>> GetAvailablePowerPlansAsync();
    Task<PowerPlan> GetActivePowerPlanAsync();
    Task<(int? acValue, int? dcValue)> GetPowerSettingACDCValuesAsync(PowerCfgSetting powerCfgSetting);
    Task<Dictionary<string, (int? acValue, int? dcValue)>> GetAllPowerSettingsACDCAsync(string powerPlanGuid = "SCHEME_CURRENT");
    Task<bool> IsSettingHardwareControlledAsync(PowerCfgSetting powerCfgSetting);
    void InvalidateCache();
}