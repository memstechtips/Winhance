using System;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class RecommendedSettingsApplier(
    IDomainServiceRouter domainServiceRouter,
    IRecommendedSettingsService recommendedSettingsService,
    ILogService logService) : IRecommendedSettingsApplier
{
    public async Task ApplyRecommendedSettingsForDomainAsync(string settingId, ISettingApplicationService settingApplicationService)
    {
        try
        {
            var domainService = domainServiceRouter.GetDomainService(settingId);
            logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] Starting to apply recommended settings for domain '{domainService.DomainName}'");

            var recommendedSettings = await recommendedSettingsService.GetRecommendedSettingsAsync(settingId).ConfigureAwait(false);
            var settingsList = recommendedSettings.ToList();

            logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] Found {settingsList.Count} recommended settings for domain '{domainService.DomainName}'");

            if (settingsList.Count == 0)
            {
                logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] No recommended settings found for domain '{domainService.DomainName}'");
                return;
            }

            foreach (var setting in settingsList)
            {
                try
                {
                    var recommendedValue = RecommendedSettingsService.GetRecommendedValueForSetting(setting);
                    logService.Log(LogLevel.Debug, $"[RecommendedSettingsApplier] Applying recommended setting '{setting.Id}' with value '{recommendedValue}'");

                    if (setting.InputType == InputType.Toggle)
                    {
                        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
                        bool enableValue = false;

                        if (registrySetting != null && recommendedValue != null)
                        {
                            enableValue = recommendedValue.Equals(registrySetting.EnabledValue);
                        }

                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = enableValue,
                            Value = recommendedValue,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }
                    else if (setting.InputType == InputType.Selection)
                    {
                        var recommendedOption = RecommendedSettingsService.GetRecommendedOptionFromSetting(setting);

                        if (recommendedOption != null)
                        {
                            var registryValue = RecommendedSettingsService.GetRegistryValueFromOptionName(setting, recommendedOption);
                            var comboBoxIndex = RecommendedSettingsService.GetCorrectSelectionIndex(setting, recommendedOption, registryValue);
                            await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = setting.Id,
                                Enable = true,
                                Value = comboBoxIndex,
                                SkipValuePrerequisites = true
                            }).ConfigureAwait(false);
                        }
                        else
                        {
                            await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = setting.Id,
                                Enable = true,
                                Value = recommendedValue,
                                SkipValuePrerequisites = true
                            }).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = true,
                            Value = recommendedValue,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }

                    logService.Log(LogLevel.Debug, $"[RecommendedSettingsApplier] Successfully applied recommended setting '{setting.Id}'");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[RecommendedSettingsApplier] Failed to apply recommended setting '{setting.Id}': {ex.Message}");
                }
            }

            logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] Completed applying recommended settings for domain '{domainService.DomainName}'");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[RecommendedSettingsApplier] Error applying recommended settings: {ex.Message}");
            throw;
        }
    }
}
