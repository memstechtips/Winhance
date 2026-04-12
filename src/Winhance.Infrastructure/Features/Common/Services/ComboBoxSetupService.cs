using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using ComboBoxOption = Winhance.Core.Features.Common.Interfaces.ComboBoxOption;

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
        if (comboBox?.Options == null || comboBox.Options.Count == 0)
            return false;

        var options = comboBox.Options;
        var supportsCustomState = comboBox.SupportsCustomState;
        var isCustomState = currentIndex == ComboBoxConstants.CustomStateIndex;

        for (int i = 0; i < options.Count; i++)
        {
            result.Options.Add(new ComboBoxOption(
                options[i].DisplayName,
                i,
                options[i].Tooltip));
        }

        if (supportsCustomState && isCustomState)
        {
            var customDisplayName = comboBox.CustomStateDisplayName ?? "Custom (User Defined)";
            result.Options.Add(new ComboBoxOption(
                customDisplayName,
                ComboBoxConstants.CustomStateIndex,
                null));
        }

        result.SelectedValue = isCustomState ? ComboBoxConstants.CustomStateIndex : currentIndex;
        return true;
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
