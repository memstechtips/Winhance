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
    private readonly Dictionary<string, (int? min, int? max)> _capabilityCache = new();

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
        _capabilityCache.Clear();
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

    public async Task<(int? acValue, int? dcValue)> GetPowerSettingACDCValuesAsync(PowerCfgSetting powerCfgSetting)
    {
        try
        {
            var command = $"powercfg /query SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid}";
            var result = await commandService.ExecuteCommandAsync(command);

            if (!result.Success || string.IsNullOrEmpty(result.Output))
                return (null, null);

            var acValue = OutputParser.PowerCfg.ParsePowerSettingValue(result.Output, "Current AC Power Setting Index:");
            var dcValue = OutputParser.PowerCfg.ParsePowerSettingValue(result.Output, "Current DC Power Setting Index:");

            return (acValue, dcValue);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error getting power setting AC/DC values: {ex.Message}");
            return (null, null);
        }
    }

    public async Task<Dictionary<string, (int? acValue, int? dcValue)>> GetAllPowerSettingsACDCAsync(string powerPlanGuid = "SCHEME_CURRENT")
    {
        try
        {
            var command = $"powercfg /query {powerPlanGuid}";
            var result = await commandService.ExecuteCommandAsync(command);

            if (!result.Success || string.IsNullOrEmpty(result.Output))
                return new Dictionary<string, (int?, int?)>();

            var bulkResults = OutputParser.PowerCfg.ParseBulkPowerSettingsOutput(result.Output);

            var acDcResults = new Dictionary<string, (int? acValue, int? dcValue)>();
            foreach (var (settingGuid, values) in bulkResults)
            {
                var ac = values.TryGetValue("AC", out var acVal) ? acVal : null;
                var dc = values.TryGetValue("DC", out var dcVal) ? dcVal : null;
                acDcResults[settingGuid] = (ac, dc);
            }

            return acDcResults;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error in GetAllPowerSettingsACDCAsync: {ex.Message}");
            return new Dictionary<string, (int?, int?)>();
        }
    }

    public async Task<(int? minValue, int? maxValue)> GetPowerSettingCapabilitiesAsync(PowerCfgSetting powerCfgSetting)
    {
        var cacheKey = powerCfgSetting.SettingGuid;

        if (_capabilityCache.TryGetValue(cacheKey, out var cached))
        {
            logService.Log(LogLevel.Debug, $"Using cached capabilities for {powerCfgSetting.SettingGUIDAlias ?? cacheKey}");
            return cached;
        }

        try
        {
            var command = $"powercfg /query SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid}";
            logService.Log(LogLevel.Debug, $"Querying power setting capabilities: {command}");

            var result = await commandService.ExecuteCommandAsync(command);

            if (!result.Success || string.IsNullOrEmpty(result.Output))
            {
                logService.Log(LogLevel.Warning, $"Failed to query capabilities for {powerCfgSetting.SettingGUIDAlias ?? cacheKey}");
                return (null, null);
            }

            var capabilities = OutputParser.PowerCfg.ParsePowerSettingMinMax(result.Output);
            _capabilityCache[cacheKey] = capabilities;

            logService.Log(LogLevel.Info,
                $"Power setting '{powerCfgSetting.SettingGUIDAlias ?? cacheKey}' capabilities: Min={capabilities.minValue}, Max={capabilities.maxValue}");

            return capabilities;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error,
                $"Error querying capabilities for {powerCfgSetting.SettingGuid}: {ex.Message}");
            return (null, null);
        }
    }

    public async Task<bool> IsSettingHardwareControlledAsync(PowerCfgSetting powerCfgSetting)
    {
        var (minValue, maxValue) = await GetPowerSettingCapabilitiesAsync(powerCfgSetting);

        bool isHardwareControlled = minValue == 0 && maxValue == 0;

        if (isHardwareControlled)
        {
            logService.Log(LogLevel.Info,
                $"Setting '{powerCfgSetting.SettingGUIDAlias ?? powerCfgSetting.SettingGuid}' is hardware-controlled (Min=0, Max=0)");
        }

        return isHardwareControlled;
    }

}