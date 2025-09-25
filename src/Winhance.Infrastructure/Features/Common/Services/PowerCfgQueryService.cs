using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class PowerCfgQueryService(ICommandService commandService, ILogService logService) : IPowerCfgQueryService
{
    private List<PowerPlan>? _cachedPlans;
    private DateTime _cacheTime;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(2);
    private Dictionary<string, int?>? _cachedCompatibleSettings;
    private DateTime _compatibleSettingsCacheTime;
    private readonly TimeSpan _compatibleSettingsCacheTimeout = TimeSpan.FromSeconds(1);

    public async Task<List<PowerPlan>> GetAvailablePowerPlansAsync()
    {
        if (_cachedPlans != null && DateTime.UtcNow - _cacheTime < _cacheTimeout)
        {
            logService.Log(LogLevel.Debug, $"[PowerCfgQueryService] Using cached power plans ({_cachedPlans.Count} plans)");
            return _cachedPlans;
        }

        try
        {
            logService.Log(LogLevel.Info, "[PowerCfgQueryService] Executing 'powercfg /list' to discover system power plans");
            var result = await commandService.ExecuteCommandAsync("powercfg /list");

            if (!result.Success || string.IsNullOrEmpty(result.Output))
            {
                logService.Log(LogLevel.Warning, "[PowerCfgQueryService] No power plans found or command failed");
                return new List<PowerPlan>();
            }

            _cachedPlans = OutputParser.PowerCfg.ParsePowerPlansFromListOutput(result.Output);
            _cacheTime = DateTime.UtcNow;

            var activePlan = _cachedPlans.FirstOrDefault(p => p.IsActive);
            logService.Log(LogLevel.Info, $"[PowerCfgQueryService] Discovered {_cachedPlans.Count} system power plans. Active: {activePlan?.Name ?? "None"} ({activePlan?.Guid ?? "N/A"})");
            
            foreach (var plan in _cachedPlans)
            {
                logService.Log(LogLevel.Debug, $"[PowerCfgQueryService]   - {plan.Name} ({plan.Guid}){(plan.IsActive ? " *ACTIVE*" : "")}");
            }

            return _cachedPlans;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerCfgQueryService] Error getting available power plans: {ex.Message}");
            return new List<PowerPlan>();
        }
    }

    public void InvalidateCache()
    {
        _cachedPlans = null;
        _cachedCompatibleSettings = null;
    }

    public async Task<PowerPlan> GetActivePowerPlanAsync()
    {
        try
        {
            var allPlans = await GetAvailablePowerPlansAsync();
            return allPlans.FirstOrDefault(p => p.IsActive) ?? new PowerPlan { Guid = "", Name = "Unknown", IsActive = true };
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting active power plan: {ex.Message}");
            return new PowerPlan { Guid = "", Name = "Unknown", IsActive = true };
        }
    }

    public async Task<PowerPlan?> GetPowerPlanByGuidAsync(string guid)
    {
        try
        {
            var availablePlans = await GetAvailablePowerPlansAsync();
            return availablePlans.FirstOrDefault(p => string.Equals(p.Guid, guid, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting power plan by GUID: {ex.Message}");
            return null;
        }
    }

    public async Task<int> GetPowerPlanIndexAsync(string guid, List<string> options)
    {
        try
        {
            var availablePlans = await GetAvailablePowerPlansAsync();
            var activePlanData = availablePlans.FirstOrDefault(p => p.IsActive);

            if (activePlanData == null)
                return 0;

            for (int i = 0; i < options.Count; i++)
            {
                var optionName = options[i].Trim();
                var matchingPlan = availablePlans.FirstOrDefault(p =>
                    p.Name.Trim().Equals(optionName, StringComparison.OrdinalIgnoreCase));

                if (matchingPlan != null && matchingPlan.Guid.Equals(activePlanData.Guid, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error resolving power plan index: {ex.Message}");
            return 0;
        }
    }

    public async Task<int?> GetPowerSettingValueAsync(PowerCfgSetting powerCfgSetting)
    {
        try
        {
            var command = $"powercfg /query SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid}";
            var result = await commandService.ExecuteCommandAsync(command);

            if (!result.Success || string.IsNullOrEmpty(result.Output))
                return null;

            return OutputParser.PowerCfg.ParsePowerSettingValue(result.Output, "Current AC Power Setting Index:");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error getting power setting value: {ex.Message}");
            return null;
        }
    }

    public async Task<Dictionary<string, int?>> GetAllPowerSettingsAsync(string powerPlanGuid = "SCHEME_CURRENT")
    {
        try
        {
            var command = $"powercfg /query {powerPlanGuid}";
            var result = await commandService.ExecuteCommandAsync(command);

            if (!result.Success || string.IsNullOrEmpty(result.Output))
                return new Dictionary<string, int?>();

            var bulkResults = OutputParser.PowerCfg.ParseBulkPowerSettingsOutput(result.Output);
            return OutputParser.PowerCfg.FlattenPowerSettings(bulkResults);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error in GetAllPowerSettingsAsync: {ex.Message}");
            return new Dictionary<string, int?>();
        }
    }

    public async Task<(Dictionary<string, int?> powerSettings, List<PowerPlan> powerPlans)> GetBulkPowerDataAsync()
    {
        var batchCommand = @"echo === POWER_PLANS_START === && powercfg /list && echo === POWER_PLANS_END === && echo === POWER_SETTINGS_START === && powercfg /query SCHEME_CURRENT && echo === POWER_SETTINGS_END ===";

        var result = await commandService.ExecuteCommandAsync(batchCommand);

        if (!result.Success || string.IsNullOrEmpty(result.Output))
        {
            return (new Dictionary<string, int?>(), new List<PowerPlan>());
        }

        try
        {
            var (powerPlans, powerSettings) = OutputParser.PowerCfg.ParseDelimitedPowerOutput(result.Output);
            return (powerSettings, powerPlans);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error parsing bulk power output: {ex.Message}");
            return (new Dictionary<string, int?>(), new List<PowerPlan>());
        }
    }

    public async Task<Dictionary<string, int?>> GetCompatiblePowerSettingsStateAsync(IEnumerable<SettingDefinition> compatibleSettings)
    {
        if (_cachedCompatibleSettings != null && DateTime.UtcNow - _compatibleSettingsCacheTime < _compatibleSettingsCacheTimeout)
        {
            return _cachedCompatibleSettings;
        }

        var results = new Dictionary<string, int?>();
        var powerSettings = compatibleSettings.Where(s => s.PowerCfgSettings?.Any() == true);

        if (!powerSettings.Any())
            return results;

        try
        {
            var command = @"powercfg /query SCHEME_CURRENT | findstr /C:""Power Setting GUID:"" /C:""Current AC Power Setting Index:"" /C:""Current DC Power Setting Index:""";
            var result = await commandService.ExecuteCommandAsync(command);

            if (!result.Success || string.IsNullOrEmpty(result.Output))
                return results;

            var parsedSettings = OutputParser.PowerCfg.ParseFilteredPowerSettingsOutput(result.Output);

            foreach (var setting in powerSettings)
            {
                var powerCfgSetting = setting.PowerCfgSettings[0];
                var key = powerCfgSetting.SettingGuid;
                if (parsedSettings.TryGetValue(key, out var value))
                {
                    results[key] = value;
                }
            }

            _cachedCompatibleSettings = results;
            _compatibleSettingsCacheTime = DateTime.UtcNow;

            return results;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error in GetCompatiblePowerSettingsAsync: {ex.Message}");
            return results;
        }
    }

}