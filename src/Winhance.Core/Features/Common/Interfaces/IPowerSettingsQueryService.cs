using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerSettingsQueryService
{
    Task<List<PowerPlan>> GetAvailablePowerPlansAsync();
    Task<PowerPlan> GetActivePowerPlanAsync();
    Task<(int? acValue, int? dcValue)> GetPowerSettingACDCValuesAsync(PowerCfgSetting powerCfgSetting);
    Task<Dictionary<string, (int? acValue, int? dcValue)>> GetAllPowerSettingsACDCAsync(string powerPlanGuid = "SCHEME_CURRENT");
    Task<Dictionary<string, (int? acValue, int? dcValue)>> GetPowerSettingsACDCAsync(IReadOnlyCollection<(string subgroupGuid, string settingGuid)> settings);
    // The choices Windows itself offers for a setting that takes one of a list, each with the name Windows shows
    // for it in the language Windows is installed in. Empty for a setting that takes a range instead, and for one
    // this machine does not have.
    Task<IReadOnlyList<PowerSettingOption>> GetPossibleValuesAsync(string subgroupGuid, string settingGuid);

    Task<bool> IsSettingHardwareControlledAsync(PowerCfgSetting powerCfgSetting);
    Task<bool> IsSettingHardwareControlledAsync(string subgroupGuid, string settingGuid);
    void InvalidateCache();
}