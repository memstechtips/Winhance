using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Common.Constants;
using Winhance.Infrastructure.Features.Optimize.Services;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SystemSettingsDiscoveryService(
        IWindowsRegistryService registryService,
        ICommandService commandService,
        ILogService logService,
        IPowerCfgQueryService powerCfgQueryService,
        IPowerSettingsValidationService powerSettingsValidationService,
        IDomainServiceRouter domainServiceRouter) : ISystemSettingsDiscoveryService
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
                var powerCfgSetting = setting.PowerCfgSettings[0];

                if (powerCfgSetting.PowerModeSupport == PowerModeSupport.Separate)
                {
                    var (acValue, dcValue) = await powerCfgQueryService.GetPowerSettingACDCValuesAsync(powerCfgSetting);
                    rawValues["ACValue"] = acValue;
                    rawValues["DCValue"] = dcValue;
                    rawValues["PowerCfgValue"] = acValue;
                }
                else
                {
                    var powerValue = await powerCfgQueryService.GetPowerSettingValueAsync(powerCfgSetting);
                    rawValues["PowerCfgValue"] = powerValue;
                }

                results[setting.Id] = rawValues;
            }
            else if (powerCfgSettings.Count > 1 || powerPlanSettings.Any())
            {
                var allPowerSettingsACDC = await powerCfgQueryService.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

                if (powerPlanSettings.Any())
                {
                    availablePlans = await powerCfgQueryService.GetAvailablePowerPlansAsync();
                }

                foreach (var setting in powerCfgSettings)
                {
                    var rawValues = new Dictionary<string, object?>();
                    var powerCfgSetting = setting.PowerCfgSettings[0];
                    var settingKey = powerCfgSetting.SettingGuid;

                    if (powerCfgSetting.PowerModeSupport == PowerModeSupport.Separate)
                    {
                        if (allPowerSettingsACDC.TryGetValue(settingKey, out var values))
                        {
                            rawValues["ACValue"] = values.acValue;
                            rawValues["DCValue"] = values.dcValue;
                            rawValues["PowerCfgValue"] = values.acValue;
                        }
                    }
                    else
                    {
                        if (allPowerSettingsACDC.TryGetValue(settingKey, out var values))
                        {
                            rawValues["PowerCfgValue"] = values.acValue;
                        }
                    }

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
                        valueName: rs.ValueName
                    )))
                    .ToList();

                var queries = registryQueries.Select(q => (q.keyPath, q.valueName)).Distinct();
                batchedRegistryValues = registryService.GetBatchValues(queries);

                foreach (var setting in registrySettings)
                {
                    var rawValues = new Dictionary<string, object?>();

                    var settingsByValueName = setting.RegistrySettings
                        .GroupBy(rs => rs.ValueName ?? "KeyExists")
                        .ToList();

                    foreach (var group in settingsByValueName)
                    {
                        var valueKey = group.Key;
                        object? finalValue = null;
                        bool foundValue = false;

                        var prioritizedSettings = group.OrderByDescending(rs =>
                            rs.KeyPath.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase));

                        foreach (var registrySetting in prioritizedSettings)
                        {
                            var resultKey = registrySetting.ValueName == null
                                ? $"{registrySetting.KeyPath}\\__KEY_EXISTS__"
                                : $"{registrySetting.KeyPath}\\{registrySetting.ValueName}";

                            if (batchedRegistryValues.TryGetValue(resultKey, out var value))
                            {
                                if (registrySetting.BitMask.HasValue && registrySetting.BinaryByteIndex.HasValue && value is byte[] binaryValue)
                                {
                                    if (binaryValue.Length > registrySetting.BinaryByteIndex.Value)
                                    {
                                        var byteValue = binaryValue[registrySetting.BinaryByteIndex.Value];
                                        var isBitSet = (byteValue & registrySetting.BitMask.Value) == registrySetting.BitMask.Value;
                                        value = isBitSet;
                                    }
                                    else
                                    {
                                        value = null;
                                    }
                                }
                                else if (registrySetting.ModifyByteOnly && registrySetting.BinaryByteIndex.HasValue && value is byte[] modifyByteValue)
                                {
                                    if (modifyByteValue.Length > registrySetting.BinaryByteIndex.Value)
                                    {
                                        value = modifyByteValue[registrySetting.BinaryByteIndex.Value];
                                    }
                                    else
                                    {
                                        value = null;
                                    }
                                }

                                if (value != null || !foundValue)
                                {
                                    finalValue = value;
                                    foundValue = true;
                                    if (value != null) break;
                                }
                            }
                        }

                        rawValues[valueKey] = finalValue;
                    }

                    results[setting.Id] = rawValues;
                }
            }

            foreach (var setting in powerPlanSettings)
            {
                var rawValues = new Dictionary<string, object?>();
                var activePlan = availablePlans.FirstOrDefault(p => p.IsActive);
                rawValues["ActivePowerPlan"] = activePlan?.Name;
                rawValues["ActivePowerPlanGuid"] = activePlan?.Guid;
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

            var settingsByDomain = settingsList
                .Where(s => s.InputType == InputType.Selection)
                .GroupBy(s => domainServiceRouter.GetDomainService(s.Id).DomainName);

            foreach (var group in settingsByDomain)
            {
                try
                {
                    var domainService = domainServiceRouter.GetDomainService(group.First().Id);
                    var discoveredValues = await domainService.DiscoverSpecialSettingsAsync(group);

                    foreach (var (settingId, values) in discoveredValues)
                    {
                        if (values.Any())
                        {
                            results[settingId] = values;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Exception discovering special settings for domain '{group.Key}': {ex.Message}");
                }
            }

            var queryType = powerCfgSettings.Count == 1 ? "Individual" : "Bulk";
            logService.Log(LogLevel.Info, $"Completed processing {results.Count} settings ({queryType}): Registry({registrySettings.Count}), PowerCfg({powerCfgSettings.Count}), Commands({commandSettings.Count}), PowerPlan({powerPlanSettings.Count}), DomainSpecial({settingsByDomain.Count()} domains)");
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

                    if (setting.InputType == InputType.Selection)
                    {
                        currentValue = ResolveRawValuesToIndex(setting, settingRawValues);
                    }
                    else if (setting.InputType == InputType.NumericRange)
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

                    bool isRegistryNotSet = false;
                    if (DebugFlags.ShowRegistryStateDebugging &&
                        setting.RegistrySettings?.Count > 0)
                    {
                        isRegistryNotSet = !registryService.RegistryValueExists(setting.RegistrySettings[0]);
                    }

                    results[setting.Id] = new SettingStateResult
                    {
                        Success = true,
                        IsEnabled = isEnabled,
                        CurrentValue = currentValue,
                        RawValues = settingRawValues,
                        IsRegistryValueNotSet = isRegistryNotSet
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

        // Need this private function as we can't inject IComboboxResolver here, it creates a circular dependency issue
        private int ResolveRawValuesToIndex(SettingDefinition setting, Dictionary<string, object?> rawValues)
        {
            if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj))
            {
                return 0;
            }

            if (rawValues.TryGetValue("CurrentPolicyIndex", out var policyIndex))
            {
                return policyIndex is int index ? index : 0;
            }

            var mappings = (Dictionary<int, Dictionary<string, object?>>)mappingsObj;
            var currentValues = new Dictionary<string, object?>();

            if (setting.PowerCfgSettings?.Count > 0 && rawValues.TryGetValue("PowerCfgValue", out var powerCfgValue))
            {
                currentValues["PowerCfgValue"] = powerCfgValue != null ? Convert.ToInt32(powerCfgValue) : null;
            }

            foreach (var registrySetting in setting.RegistrySettings)
            {
                var key = registrySetting.ValueName ?? "KeyExists";
                if (rawValues.TryGetValue(key, out var rawValue) && rawValue != null)
                {
                    currentValues[key] = rawValue;
                }
                else if (registrySetting.DefaultValue != null)
                {
                    currentValues[key] = registrySetting.DefaultValue;
                }
                else
                {
                    currentValues[key] = null;
                }
            }

            foreach (var mapping in mappings)
            {
                var index = mapping.Key;
                var expectedValues = mapping.Value;

                bool allMatch = true;
                foreach (var expectedValue in expectedValues)
                {
                    if (!currentValues.TryGetValue(expectedValue.Key, out var currentValue))
                    {
                        currentValue = null;
                    }

                    if (!ValuesAreEqual(currentValue, expectedValue.Value))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch && expectedValues.Count > 0)
                {
                    return index;
                }
            }

            var supportsCustomState = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.SupportsCustomState, out var supports) == true && (bool)supports;
            if (supportsCustomState)
            {
                return -1;
            }

            return 0;
        }

        private static bool ValuesAreEqual(object? value1, object? value2)
        {
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;

            if (value1 is byte[] bytes1 && value2 is byte[] bytes2)
            {
                return bytes1.SequenceEqual(bytes2);
            }

            if (value1 is byte b1 && value2 is byte b2)
            {
                return b1 == b2;
            }

            if (value1 is byte b1Int && value2 is int i2)
            {
                return b1Int == i2;
            }

            if (value1 is int i1 && value2 is byte b2Int)
            {
                return i1 == b2Int;
            }

            return value1.Equals(value2);
        }
    }
}