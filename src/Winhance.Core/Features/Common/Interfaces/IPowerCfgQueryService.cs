using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerCfgQueryService
{
    Task<List<PowerPlan>> GetAvailablePowerPlansAsync();
    Task<PowerPlan> GetActivePowerPlanAsync();
    Task<PowerPlan?> GetPowerPlanByGuidAsync(string guid);
    Task<int> GetPowerPlanIndexAsync(string guid, List<string> options);
    Task<int?> GetPowerSettingValueAsync(PowerCfgSetting powerCfgSetting);
    Task<Dictionary<string, int?>> GetAllPowerSettingsAsync(string powerPlanGuid = "SCHEME_CURRENT");
    Task<(Dictionary<string, int?> powerSettings, List<PowerPlan> powerPlans)> GetBulkPowerDataAsync();
    Task<Dictionary<string, int?>> GetCompatiblePowerSettingsStateAsync(IEnumerable<SettingDefinition> compatibleSettings);
    void InvalidateCache();
}