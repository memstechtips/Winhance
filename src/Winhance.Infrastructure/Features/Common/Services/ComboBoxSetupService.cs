using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class ComboBoxSetupService(
        ILogService logService,
        IComboBoxResolver comboBoxResolver,
        IPowerPlanComboBoxService powerPlanComboBoxService,
        ISystemSettingsDiscoveryService systemSettingsDiscoveryService) : IComboBoxSetupService
    {
        public ComboBoxSetupResult SetupComboBoxOptions(SettingDefinition setting, object? currentValue)
        {
            return SetupComboBoxOptionsAsync(setting, currentValue).GetAwaiter().GetResult();
        }

        public async Task<ComboBoxSetupResult> SetupComboBoxOptionsAsync(SettingDefinition setting, object? currentValue)
        {
            var result = new ComboBoxSetupResult();

            try
            {
                if (setting.InputType != InputType.Selection)
                {
                    result.ErrorMessage = $"Setting '{setting.Id}' is not a ComboBox control";
                    return result;
                }

                if (setting.Id == "power-plan-selection")
                {
                    return await powerPlanComboBoxService.SetupPowerPlanComboBoxAsync(setting, currentValue).ConfigureAwait(false);
                }

                int currentIndex = 0;
                if (currentValue is int indexValue)
                {
                    currentIndex = indexValue;
                }
                else
                {
                    var rawValues = await systemSettingsDiscoveryService.GetRawSettingsValuesAsync(new[] { setting }).ConfigureAwait(false);
                    var rawSettingValues = rawValues.TryGetValue(setting.Id, out var values) ? values : new Dictionary<string, object?>();
                    currentIndex = comboBoxResolver.ResolveRawValuesToIndex(setting, rawSettingValues);
                }

                if (SetupFromComboBoxDisplayNames(setting, currentIndex, result))
                {
                    result.Success = true;
                    return result;
                }

                result.ErrorMessage = $"Invalid ComboBox metadata for setting '{setting.Id}'";
                logService.Log(LogLevel.Warning, result.ErrorMessage);
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error setting up ComboBox for '{setting.Id}': {ex.Message}";
                logService.Log(LogLevel.Error, result.ErrorMessage);
                return result;
            }
        }

        private bool SetupFromComboBoxDisplayNames(SettingDefinition setting, int currentIndex, ComboBoxSetupResult result)
        {
            if (!setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ComboBoxDisplayNames) == true ||
                !setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ValueMappings) == true)
            {
                return false;
            }

            var displayNames = setting.CustomProperties![CustomPropertyKeys.ComboBoxDisplayNames] as string[];
            var valueMappings = setting.CustomProperties![CustomPropertyKeys.ValueMappings] as Dictionary<int, Dictionary<string, object?>>;

            if (displayNames == null || valueMappings == null)
            {
                var simpleValueMappings = setting.CustomProperties![CustomPropertyKeys.ValueMappings] as Dictionary<int, int>;
                if (displayNames != null && simpleValueMappings != null)
                {
                    return SetupFromSimpleValueMappings(setting, currentIndex, result, displayNames, simpleValueMappings);
                }
                
                var commandValueMappings = setting.CustomProperties[CustomPropertyKeys.ValueMappings] as Dictionary<int, bool>;
                if (displayNames != null && commandValueMappings != null)
                {
                    return SetupFromCommandValueMappings(setting, currentIndex, result, displayNames, commandValueMappings);
                }
                
                return false;
            }

            var supportsCustomState = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.SupportsCustomState, out var supports) == true && (bool)supports;
            var isCustomState = currentIndex == ComboBoxConstants.CustomStateIndex;

            string[] finalDisplayNames = displayNames;

            if (supportsCustomState && isCustomState)
            {
                var customDisplayName = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.CustomStateDisplayName, out var customName) == true && customName is string customStr
                    ? customStr
                    : "Custom (User Defined)";

                finalDisplayNames = displayNames.Append(customDisplayName).ToArray();
            }

            string[]? optionTooltips = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.OptionTooltips, out var tooltips) == true
                ? tooltips as string[]
                : null;

            for (int i = 0; i < finalDisplayNames.Length; i++)
            {
                result.Options.Add(new ComboBoxOption
                {
                    DisplayText = finalDisplayNames[i],
                    Value = i < displayNames.Length ? i : ComboBoxConstants.CustomStateIndex,
                    Description = optionTooltips != null && i < optionTooltips.Length ? optionTooltips[i] : null
                });
            }

            result.SelectedValue = isCustomState ? ComboBoxConstants.CustomStateIndex : currentIndex;
            return true;
        }

        private bool SetupFromSimpleValueMappings(SettingDefinition setting, int currentIndex, ComboBoxSetupResult result, string[] displayNames, Dictionary<int, int> simpleValueMappings)
        {
            try
            {
                var supportsCustomState = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.SupportsCustomState, out var supports) == true && (bool)supports;
                var isCustomState = currentIndex == ComboBoxConstants.CustomStateIndex;
                
                string[] finalDisplayNames = displayNames;
                
                if (supportsCustomState && isCustomState)
                {
                    var customDisplayName = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.CustomStateDisplayName, out var customName) == true && customName is string customStr
                        ? customStr
                        : "Custom (User Defined)";

                    finalDisplayNames = displayNames.Append(customDisplayName).ToArray();
                }

                string[]? optionTooltips = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.OptionTooltips, out var tooltips) == true
                    ? tooltips as string[]
                    : null;

                for (int i = 0; i < finalDisplayNames.Length; i++)
                {
                    result.Options.Add(new ComboBoxOption
                    {
                        DisplayText = finalDisplayNames[i],
                        Value = i < displayNames.Length ? i : ComboBoxConstants.CustomStateIndex,
                        Description = optionTooltips != null && i < optionTooltips.Length ? optionTooltips[i] : null
                    });
                }

                result.SelectedValue = isCustomState ? ComboBoxConstants.CustomStateIndex : currentIndex;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool SetupFromCommandValueMappings(SettingDefinition setting, int currentIndex, ComboBoxSetupResult result, string[] displayNames, Dictionary<int, bool> commandValueMappings)
        {
            try
            {
                var supportsCustomState = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.SupportsCustomState, out var supports) == true && (bool)supports;
                var isCustomState = currentIndex == ComboBoxConstants.CustomStateIndex;

                string[] finalDisplayNames = displayNames;

                if (supportsCustomState && isCustomState)
                {
                    var customDisplayName = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.CustomStateDisplayName, out var customName) == true && customName is string customStr
                        ? customStr
                        : "Custom (User Defined)";

                    finalDisplayNames = displayNames.Append(customDisplayName).ToArray();
                }

                string[]? optionTooltips = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.OptionTooltips, out var tooltips) == true
                    ? tooltips as string[]
                    : null;

                for (int i = 0; i < finalDisplayNames.Length; i++)
                {
                    result.Options.Add(new ComboBoxOption
                    {
                        DisplayText = finalDisplayNames[i],
                        Value = i < displayNames.Length ? i : ComboBoxConstants.CustomStateIndex,
                        Description = optionTooltips != null && i < optionTooltips.Length ? optionTooltips[i] : null
                    });
                }

                result.SelectedValue = isCustomState ? ComboBoxConstants.CustomStateIndex : currentIndex;
                return true;
            }
            catch
            {
                return false;
            }
        }






        public int ResolveIndexFromRawValues(SettingDefinition setting, Dictionary<string, object?> rawValues)
        {
            try
            {
                if (setting.Id == "power-plan-selection")
                {
                    return powerPlanComboBoxService.ResolveIndexFromRawValues(setting, rawValues);
                }
                
                return comboBoxResolver.ResolveRawValuesToIndex(setting, rawValues);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to resolve index from raw values for '{setting.Id}': {ex.Message}");
                return 0;
            }
        }
    }
}