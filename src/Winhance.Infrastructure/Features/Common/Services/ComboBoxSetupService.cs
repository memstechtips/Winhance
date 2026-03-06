using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ComboBoxSetupService(
    ILogService logService,
    IComboBoxResolver comboBoxResolver,
    IPowerPlanComboBoxService powerPlanComboBoxService,
    ISystemSettingsDiscoveryService systemSettingsDiscoveryService) : IComboBoxSetupService
{
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

            if (setting.Id == SettingIds.PowerPlanSelection)
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
        var comboBox = setting.ComboBox;
        if (comboBox == null)
            return false;

        var displayNames = comboBox.DisplayNames;

        if (comboBox.ValueMappings == null)
        {
            if (comboBox.SimpleValueMappings != null || comboBox.CommandValueMappings != null)
            {
                return SetupFromMappings(setting, currentIndex, result, displayNames);
            }

            return false;
        }

        var supportsCustomState = comboBox.SupportsCustomState;
        var isCustomState = currentIndex == ComboBoxConstants.CustomStateIndex;

        string[] finalDisplayNames = displayNames;

        if (supportsCustomState && isCustomState)
        {
            var customDisplayName = comboBox.CustomStateDisplayName ?? "Custom (User Defined)";
            finalDisplayNames = displayNames.Append(customDisplayName).ToArray();
        }

        var optionTooltips = comboBox.OptionTooltips;

        for (int i = 0; i < finalDisplayNames.Length; i++)
        {
            result.Options.Add(new ComboBoxOption(
                finalDisplayNames[i],
                i < displayNames.Length ? i : ComboBoxConstants.CustomStateIndex,
                optionTooltips != null && i < optionTooltips.Length ? optionTooltips[i] : null));
        }

        result.SelectedValue = isCustomState ? ComboBoxConstants.CustomStateIndex : currentIndex;
        return true;
    }

    private bool SetupFromMappings(SettingDefinition setting, int currentIndex, ComboBoxSetupResult result, string[] displayNames)
    {
        try
        {
            var comboBox = setting.ComboBox;
            var supportsCustomState = comboBox?.SupportsCustomState == true;
            var isCustomState = currentIndex == ComboBoxConstants.CustomStateIndex;

            string[] finalDisplayNames = displayNames;

            if (supportsCustomState && isCustomState)
            {
                var customDisplayName = comboBox?.CustomStateDisplayName ?? "Custom (User Defined)";
                finalDisplayNames = displayNames.Append(customDisplayName).ToArray();
            }

            var optionTooltips = comboBox?.OptionTooltips;

            for (int i = 0; i < finalDisplayNames.Length; i++)
            {
                result.Options.Add(new ComboBoxOption(
                    finalDisplayNames[i],
                    i < displayNames.Length ? i : ComboBoxConstants.CustomStateIndex,
                    optionTooltips != null && i < optionTooltips.Length ? optionTooltips[i] : null));
            }

            result.SelectedValue = isCustomState ? ComboBoxConstants.CustomStateIndex : currentIndex;
            return true;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Failed to setup value mappings for '{setting.Id}': {ex.Message}");
            return false;
        }
    }



    public async Task<int> ResolveIndexFromRawValuesAsync(SettingDefinition setting, Dictionary<string, object?> rawValues)
    {
        try
        {
            if (setting.Id == SettingIds.PowerPlanSelection)
            {
                return await powerPlanComboBoxService.ResolveIndexFromRawValuesAsync(setting, rawValues).ConfigureAwait(false);
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
