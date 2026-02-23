using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

public class PowerSettingsValidationService(
    ILogService logService,
    IPowerSettingsQueryService powerSettingsQueryService,
    IWindowsRegistryService registryService) : IPowerSettingsValidationService
{
    public async Task<IEnumerable<SettingDefinition>> FilterSettingsByExistenceAsync(IEnumerable<SettingDefinition> settings)
    {
        var settingsList = settings.ToList();
        var originalCount = settingsList.Count;

        var bulkPowerValues = await powerSettingsQueryService.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

        if (!bulkPowerValues.Any())
        {
            logService.Log(LogLevel.Warning, "Could not get bulk power settings, skipping validation");
            return settingsList;
        }

        var validatedSettings = new List<SettingDefinition>();

        foreach (var setting in settingsList)
        {
            if (!setting.ValidateExistence || setting.PowerCfgSettings?.Any() != true)
            {
                validatedSettings.Add(setting);
                continue;
            }

            var hasValidPowerCfgSetting = false;

            foreach (var powerCfgSetting in setting.PowerCfgSettings)
            {
                var settingKey = powerCfgSetting.SettingGuid;

                if (bulkPowerValues.ContainsKey(settingKey))
                {
                    hasValidPowerCfgSetting = true;
                    break;
                }

                if (powerCfgSetting.EnablementRegistrySetting != null)
                {
                    logService.Log(LogLevel.Info, $"Attempting to enable hidden power setting: {settingKey}");

                    if (registryService.ApplySetting(powerCfgSetting.EnablementRegistrySetting, true))
                    {
                        logService.Log(LogLevel.Info, $"Successfully enabled hidden power setting: {settingKey}");

                        await Task.Delay(100);
                        var updatedPowerValues = await powerSettingsQueryService.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

                        if (updatedPowerValues.ContainsKey(settingKey))
                        {
                            hasValidPowerCfgSetting = true;
                            break;
                        }
                    }
                    else
                    {
                        logService.Log(LogLevel.Warning, $"Failed to enable hidden power setting: {settingKey}");
                    }
                }
            }

            if (hasValidPowerCfgSetting)
            {
                var shouldFilterOutDueToHardwareControl = false;

                foreach (var powerCfgSetting in setting.PowerCfgSettings.Where(p => p.CheckForHardwareControl))
                {
                    if (await powerSettingsQueryService.IsSettingHardwareControlledAsync(powerCfgSetting))
                    {
                        logService.Log(LogLevel.Info,
                            $"Filtering out hardware-controlled setting: {setting.Id} ({powerCfgSetting.SettingGUIDAlias})");
                        shouldFilterOutDueToHardwareControl = true;
                        break;
                    }
                }

                if (!shouldFilterOutDueToHardwareControl)
                {
                    validatedSettings.Add(setting);
                }
            }
        }

        var filteredCount = originalCount - validatedSettings.Count;
        if (filteredCount > 0)
        {
            logService.Log(LogLevel.Debug, $"Filtered out {filteredCount} non-existent power settings");
        }

        return validatedSettings;
    }
}
