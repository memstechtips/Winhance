using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SystemSettingsDiscoveryService(
        IWindowsRegistryService registryService,
        ICommandService commandService,
        ILogService logService,
        IPowerCfgQueryService powerCfgQueryService,
        IPowerSettingsValidationService powerSettingsValidationService) : ISystemSettingsDiscoveryService
    {
        public async Task<Dictionary<string, Dictionary<string, object?>>> GetRawSettingsValuesAsync(IEnumerable<SettingDefinition> settings)
        {
            var results = new Dictionary<string, Dictionary<string, object?>>();
            if (settings == null) return results;

            var settingsList = settings.ToList();
            var powerCfgSettings = settingsList.Where(s => s.PowerCfgSettings?.Count > 0 && s.Id != "power-plan-selection").ToList();
            var registrySettings = settingsList.Where(s => s.RegistrySettings?.Count > 0).ToList();
            var commandSettings = settingsList.Where(s => s.CommandSettings?.Count > 0).ToList();
            var powerPlanSettings = settingsList.Where(s => s.Id == "power-plan-selection").ToList();

            List<PowerPlan> availablePlans = new();

            if (powerCfgSettings.Count == 1)
            {
                var setting = powerCfgSettings[0];
                var rawValues = new Dictionary<string, object?>();
                var powerValue = await powerCfgQueryService.GetPowerSettingValueAsync(setting.PowerCfgSettings[0]);
                rawValues["PowerCfgValue"] = powerValue;
                results[setting.Id] = rawValues;
            }
            else if (powerCfgSettings.Count > 1 || powerPlanSettings.Any())
            {
                var powerSettings = await powerCfgQueryService.GetCompatiblePowerSettingsStateAsync(powerCfgSettings);
                
                if (powerPlanSettings.Any())
                {
                    availablePlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                }
                
                foreach (var setting in powerCfgSettings)
                {
                    var rawValues = new Dictionary<string, object?>();
                    var settingKey = setting.PowerCfgSettings[0].SettingGuid;
                    rawValues["PowerCfgValue"] = powerSettings.TryGetValue(settingKey, out var powerValue) ? powerValue : null;
                    results[setting.Id] = rawValues;
                }
            }

            Dictionary<string, object?> batchedRegistryValues = new();
            if (registrySettings.Any())
            {
                var registryQueries = registrySettings
                    .SelectMany(s => s.RegistrySettings.Select(rs => (
                        setting: s,
                        keyPath: rs.KeyPath,
                        valueName: rs.ValueName ?? string.Empty
                    )))
                    .ToList();

                var queries = registryQueries.Select(q => (q.keyPath, q.valueName)).Distinct();
                batchedRegistryValues = registryService.GetBatchValues(queries);

                foreach (var setting in registrySettings)
                {
                    var rawValues = new Dictionary<string, object?>();
                    
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        var resultKey = string.IsNullOrEmpty(registrySetting.ValueName)
                            ? $"{registrySetting.KeyPath}\\__KEY_EXISTS__"
                            : $"{registrySetting.KeyPath}\\{registrySetting.ValueName}";
                            
                        if (batchedRegistryValues.TryGetValue(resultKey, out var value))
                        {
                            rawValues[string.IsNullOrEmpty(registrySetting.ValueName) ? "KeyExists" : registrySetting.ValueName] = value;
                        }
                    }
                    
                    results[setting.Id] = rawValues;
                }
            }

            foreach (var setting in powerPlanSettings)
            {
                var rawValues = new Dictionary<string, object?>();
                var activePlan = availablePlans.FirstOrDefault(p => p.IsActive);
                rawValues["ActivePowerPlan"] = activePlan?.Name;
                results[setting.Id] = rawValues;
            }

            foreach (var setting in commandSettings)
            {
                try
                {
                    var rawValues = new Dictionary<string, object?>();
                    var commandSetting = setting.CommandSettings[0];
                    var isEnabled = setting.Id == "power-hibernation-enable"
                        ? await powerSettingsValidationService.IsHibernationEnabledAsync()
                        : await commandService.IsCommandSettingEnabledAsync(commandSetting);
                    rawValues["CommandEnabled"] = isEnabled;
                    results[setting.Id] = rawValues;
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Exception getting command value for '{setting.Id}': {ex.Message}");
                    results[setting.Id] = new Dictionary<string, object?>();
                }
            }

            var queryType = powerCfgSettings.Count == 1 ? "Individual" : "Bulk";
            logService.Log(LogLevel.Info, $"Completed processing {results.Count} settings ({queryType}): Registry({registrySettings.Count}), PowerCfg({powerCfgSettings.Count}), Commands({commandSettings.Count}), PowerPlan({powerPlanSettings.Count})");
            return results;
        }

        public async Task<Dictionary<string, SettingStateResult>> GetSettingStatesAsync(IEnumerable<SettingDefinition> settings)
        {
            var settingsList = settings.ToList();
            logService.Log(LogLevel.Info, $"[SystemSettingsDiscoveryService] Getting interpreted states for {settingsList.Count} settings");
            
            var allRawValues = await GetRawSettingsValuesAsync(settingsList);
            var results = new Dictionary<string, SettingStateResult>();

            foreach (var setting in settingsList)
            {
                try
                {
                    var settingRawValues = allRawValues.TryGetValue(setting.Id, out var values) 
                        ? values 
                        : new Dictionary<string, object?>();

                    bool isEnabled = DetermineIfSettingIsEnabled(setting, settingRawValues);
                    object? currentValue = null;

                    if (setting.InputType == InputType.NumericRange)
                    {
                        if (setting.PowerCfgSettings?.Count > 0)
                        {
                            currentValue = settingRawValues.TryGetValue("PowerCfgValue", out var powerValue) ? powerValue : null;
                        }
                        else if (setting.RegistrySettings?.Count > 0)
                        {
                            currentValue = settingRawValues.Values.FirstOrDefault();
                        }
                        else if (setting.CommandSettings?.Count > 0)
                        {
                            currentValue = settingRawValues.TryGetValue("CommandEnabled", out var commandEnabled) ? commandEnabled : null;
                        }
                    }

                    results[setting.Id] = new SettingStateResult
                    {
                        Success = true,
                        IsEnabled = isEnabled,
                        CurrentValue = currentValue,
                        RawValues = settingRawValues
                    };
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[SystemSettingsDiscoveryService] Error getting state for setting '{setting.Id}': {ex.Message}");
                    results[setting.Id] = new SettingStateResult { Success = false, ErrorMessage = ex.Message };
                }
            }

            logService.Log(LogLevel.Info, $"[SystemSettingsDiscoveryService] Interpreted states completed for {results.Count} settings");
            return results;
        }

        private bool DetermineIfSettingIsEnabled(SettingDefinition setting, Dictionary<string, object?> rawValues)
        {
            if (rawValues == null || rawValues.Count == 0)
                return false;

            if (setting.RegistrySettings?.Count > 0)
            {
                // Use the existing registry service that properly handles AbsenceMeansEnabled
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    if (registryService.IsSettingApplied(registrySetting))
                        return true;
                }
                return false;
            }
            else if (setting.PowerCfgSettings?.Count > 0)
            {
                if (rawValues.TryGetValue("PowerCfgValue", out var value))
                {
                    return value != null && !value.Equals(0);
                }
                return false;
            }
            else if (setting.CommandSettings?.Count > 0)
            {
                if (rawValues.TryGetValue("CommandEnabled", out var value))
                {
                    return value is bool boolValue && boolValue;
                }
                return false;
            }

            return false;
        }
    }
}