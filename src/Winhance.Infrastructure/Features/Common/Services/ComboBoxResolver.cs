using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class ComboBoxResolver(
        ISystemSettingsDiscoveryService discoveryService) : IComboBoxResolver
    {


        public async Task<object?> ResolveCurrentValueAsync(SettingDefinition setting, Dictionary<string, object?>? existingRawValues = null)
        {
            var rawValues = await GetRawValues(setting, existingRawValues).ConfigureAwait(false);
            
            if (setting.InputType == InputType.Selection && setting.ComboBox?.ValueMappings != null)
            {
                return ResolveRawValuesToIndex(setting, rawValues);
            }
            else if (setting.RegistrySettings?.Count > 0)
            {
                return rawValues.Values.FirstOrDefault();
            }
            else if (setting.ScheduledTaskSettings?.Count > 0)
            {
                return rawValues.TryGetValue("ScheduledTaskEnabled", out var taskEnabled) ? taskEnabled : null;
            }

            return null;
        }

        private async Task<Dictionary<string, object?>> GetRawValues(SettingDefinition setting, Dictionary<string, object?>? existingRawValues)
        {
            if (existingRawValues != null)
            {
                return existingRawValues;
            }

            var rawValues = await discoveryService.GetRawSettingsValuesAsync(new[] { setting }).ConfigureAwait(false);
            return rawValues.TryGetValue(setting.Id, out var values) ? values : new Dictionary<string, object?>();
        }

        public int GetValueFromIndex(SettingDefinition setting, int index)
        {
            if (index == ComboBoxConstants.CustomStateIndex)
            {
                return 0;
            }

            if (setting.ComboBox?.ValueMappings == null)
            {
                return index;
            }

            if (setting.ComboBox.ValueMappings.TryGetValue(index, out var valueDict))
            {
                var firstValue = valueDict.Values.FirstOrDefault();
                return firstValue is int intVal ? intVal : (firstValue != null ? Convert.ToInt32(firstValue) : index);
            }

            return index;
        }



        public int ResolveRawValuesToIndex(SettingDefinition setting, Dictionary<string, object?> rawValues)
        {
            if (setting.ComboBox?.ValueMappings == null)
            {
                return 0;
            }

            if (rawValues.TryGetValue("CurrentPolicyIndex", out var policyIndex))
            {
                return policyIndex is int index ? index : 0;
            }

            var mappings = setting.ComboBox.ValueMappings;
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

            var supportsCustomState = setting.ComboBox?.SupportsCustomState == true;
            if (supportsCustomState)
            {
                return ComboBoxConstants.CustomStateIndex;
            }

            return 0;
        }

        public Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index)
        {
            var result = new Dictionary<string, object?>();

            if (setting.ComboBox?.ValueMappings == null)
            {
                return result;
            }

            if (setting.ComboBox.ValueMappings.TryGetValue(index, out var expectedValues))
            {
                foreach (var expectedValue in expectedValues)
                {
                    result[expectedValue.Key] = expectedValue.Value;
                }
            }

            return result;
        }

        public int GetIndexFromDisplayName(SettingDefinition setting, string displayName)
        {
            if (setting.ComboBox?.DisplayNames is { } displayNames)
            {
                for (int i = 0; i < displayNames.Length; i++)
                {
                    if (string.Equals(displayNames[i], displayName, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            return 0;
        }

        private static bool ValuesAreEqual(object? value1, object? value2)
            => Utilities.ValueComparer.ValuesAreEqual(value1, value2);
    }
}