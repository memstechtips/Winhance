using System.IO;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Utils;

namespace Winhance.Infrastructure.Features.Common.Services;

public class PowerSettingsValidationService(
    ICommandService commandService,
    ILogService logService,
    IPowerCfgQueryService powerCfgQueryService,
    IWindowsRegistryService registryService) : IPowerSettingsValidationService
{
    public async Task<IEnumerable<SettingDefinition>> FilterSettingsByExistenceAsync(IEnumerable<SettingDefinition> settings)
    {
        var settingsList = settings.ToList();
        var originalCount = settingsList.Count;

        var bulkPowerValues = await powerCfgQueryService.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

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
                        var updatedPowerValues = await powerCfgQueryService.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

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
                validatedSettings.Add(setting);
            }
        }

        var filteredCount = originalCount - validatedSettings.Count;
        if (filteredCount > 0)
        {
            logService.Log(LogLevel.Debug, $"Filtered out {filteredCount} non-existent power settings");
        }

        return validatedSettings;
    }

    public async Task<bool> IsHibernationEnabledAsync()
    {
        try
        {
            if (File.Exists(@"C:\hiberfil.sys"))
            {
                return true;
            }

            return await CheckHibernationFromPowercfgA();
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error checking hibernation: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckHibernationFromPowercfgA()
    {
        var result = await commandService.ExecuteCommandAsync("powercfg /a");
        if (!result.Success || string.IsNullOrEmpty(result.Output))
        {
            return false;
        }

        var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        bool inAvailableSection = false;

        var hibernationKeywords = new[] {
            "hibernate", "hibernation", "ruhezustand", "hibernación",
            "hibernação", "ibernazione", "slaapstand"
        };

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim().ToLowerInvariant();

            if (trimmedLine.Contains("available on this system") && !trimmedLine.Contains("not available"))
            {
                inAvailableSection = true;
                continue;
            }
            else if (trimmedLine.Contains("not available on this system"))
            {
                inAvailableSection = false;
                continue;
            }

            if (inAvailableSection)
            {
                foreach (var keyword in hibernationKeywords)
                {
                    if (trimmedLine.Contains(keyword))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}