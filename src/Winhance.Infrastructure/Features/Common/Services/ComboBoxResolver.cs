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
        ISystemSettingsDiscoveryService discoveryService,
        ILogService logService) : IComboBoxResolver
    {
        public const int CUSTOM_STATE_INDEX = -1;

        public async Task<object?> ResolveCurrentValueAsync(SettingDefinition setting, Dictionary<string, object?>? existingRawValues = null)
        {
            var rawValues = await GetRawValues(setting, existingRawValues);
            
            if (setting.InputType == InputType.Selection && setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ValueMappings) == true)
            {
                return ResolveRawValuesToIndex(setting, rawValues);
            }
            else if (setting.RegistrySettings?.Count > 0)
            {
                return rawValues.Values.FirstOrDefault();
            }
            else if (setting.CommandSettings?.Count > 0)
            {
                return rawValues.TryGetValue("CommandEnabled", out var commandEnabled) ? commandEnabled : null;
            }

            return null;
        }

        private async Task<Dictionary<string, object?>> GetRawValues(SettingDefinition setting, Dictionary<string, object?>? existingRawValues)
        {
            if (existingRawValues != null)
            {
                return existingRawValues;
            }

            var rawValues = await discoveryService.GetRawSettingsValuesAsync(new[] { setting });
            return rawValues.TryGetValue(setting.Id, out var values) ? values : new Dictionary<string, object?>();
        }

        public int GetValueFromIndex(SettingDefinition setting, int index)
        {
            if (index == CUSTOM_STATE_INDEX)
            {
                return 0;
            }

            if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj))
            {
                return index;
            }

            var mappings = (Dictionary<int, Dictionary<string, int?>>)mappingsObj;
            if (mappings.TryGetValue(index, out var valueDict))
            {
                var firstValue = valueDict.Values.FirstOrDefault();
                return firstValue ?? index;
            }

            return index;
        }



        public int ResolveRawValuesToIndex(SettingDefinition setting, Dictionary<string, object?> rawValues)
        {
            if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj))
            {
                return 0;
            }

            var mappings = (Dictionary<int, Dictionary<string, int?>>)mappingsObj;
            var currentValues = new Dictionary<string, int?>();
            foreach (var rawValue in rawValues)
            {
                var intValue = rawValue.Value != null ? Convert.ToInt32(rawValue.Value) : (int?)null;
                currentValues[rawValue.Key] = intValue;
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
                    
                    if (!Equals(currentValue, expectedValue.Value))
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
                return CUSTOM_STATE_INDEX;
            }

            return 0;
        }

        public Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index)
        {
            var result = new Dictionary<string, object?>();

            if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj))
            {
                return result;
            }

            var mappings = (Dictionary<int, Dictionary<string, int?>>)mappingsObj;
            if (mappings.TryGetValue(index, out var expectedValues))
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
            if (setting.CustomProperties?.TryGetValue(CustomPropertyKeys.ComboBoxDisplayNames, out var displayNamesObj) == true &&
                displayNamesObj is string[] displayNames)
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
    }
}