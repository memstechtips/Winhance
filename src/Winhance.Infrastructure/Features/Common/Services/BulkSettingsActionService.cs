using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Common.Services;

public class BulkSettingsActionService(
    ICompatibleSettingsRegistry settingsRegistry,
    IWindowsVersionService versionService,
    ISettingApplicationService settingApplicationService,
    IProcessRestartManager processRestartManager,
    IRecommendedSettingsApplier recommendedSettingsApplier,
    ILogService logService) : IBulkSettingsActionService
{
    public async Task<int> ApplyRecommendedAsync(
        IEnumerable<string> settingIds,
        IProgress<TaskProgressDetail>? progress = null)
    {
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        var applied = await recommendedSettingsApplier
            .ApplyRecommendedToSettingsAsync(settings, settingApplicationService, progress).ConfigureAwait(false);
        await processRestartManager.FlushCoalescedRestartsAsync(applied).ConfigureAwait(false);
        progress?.Report(new TaskProgressDetail { Progress = 100, StatusText = $"Applied {applied.Count} of {settings.Count} settings", IsCompletion = true, IsActive = false });
        return applied.Count;
    }

    public async Task<int> ResetToDefaultsAsync(
        IEnumerable<string> settingIds,
        IProgress<TaskProgressDetail>? progress = null)
    {
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        int applied = 0;
        int total = settings.Count;
        var appliedForRestart = new List<SettingDefinition>(total);

        using (processRestartManager.SuppressRestarts())
        {
        for (int i = 0; i < total; i++)
        {
            var setting = settings[i];
            try
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = (double)i / total * 100,
                    StatusText = $"Resetting to default: {setting.Name}",
                    QueueCurrent = i + 1,
                    QueueTotal = total,
                    IsActive = true
                });

                if (setting.InputType == InputType.Toggle)
                {
                    var toggleState = SettingDefinitionToggleState.GetDefaultToggleState(setting);
                    if (toggleState is not bool enableValue)
                    {
                        logService.Log(LogLevel.Debug, $"[BulkSettings] Skipping '{setting.Id}' - no default toggle state");
                        continue;
                    }

                    // Mirror per-card HandleToggleAsync: pass only SettingId + Enable + ResetToDefault.
                    // The apply pipeline derives the registry write from EnabledValue/DisabledValue.
                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = enableValue,
                        ResetToDefault = true,
                        SkipValuePrerequisites = true
                    }).ConfigureAwait(false);
                }
                else if (setting.InputType == InputType.Selection)
                {
                    var powerCfgValue = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: false);
                    if (powerCfgValue != null)
                    {
                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = true,
                            Value = powerCfgValue,
                            ResetToDefault = true,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        var defaultIndex = RecommendedSettingsResolver.GetDefaultIndex(setting);
                        if (defaultIndex is int idx)
                        {
                            await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = setting.Id,
                                Enable = true,
                                Value = idx,
                                ResetToDefault = true,
                                SkipValuePrerequisites = true
                            }).ConfigureAwait(false);
                        }
                        // No else: catalog validator guarantees every registry-backed Selection has
                        // exactly one IsDefault option.
                    }
                }
                else
                {
                    var valueToApply = RecommendedSettingsResolver.GetDefaultValueForSetting(setting)
                        ?? RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: false);
                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = valueToApply != null,
                        Value = valueToApply,
                        ResetToDefault = true,
                        SkipValuePrerequisites = true
                    }).ConfigureAwait(false);
                }

                applied++;
                appliedForRestart.Add(setting);
                logService.Log(LogLevel.Debug, $"[BulkSettings] Reset to default for '{setting.Id}'");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to reset default for '{setting.Id}': {ex.Message}");
            }
        }
        } // end SuppressRestarts scope

        await processRestartManager.FlushCoalescedRestartsAsync(appliedForRestart).ConfigureAwait(false);

        progress?.Report(new TaskProgressDetail
        {
            Progress = 100,
            StatusText = $"Reset {applied} of {total} settings",
            IsCompletion = true,
            IsActive = false
        });

        return applied;
    }

    public async Task<int> GetAffectedCountAsync(
        IEnumerable<string> settingIds,
        BulkActionType actionType)
    {
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        int count = 0;

        foreach (var setting in settings)
        {
            try
            {
                // Counter must agree with the apply path. HasRecommendedValue / HasDefaultValue
                // route through SettingDefinitionToggleState so the count never reports a
                // setting that the loop above would silently skip.
                bool wouldChange = actionType switch
                {
                    BulkActionType.ApplyRecommended => RecommendedSettingsResolver.HasRecommendedValue(setting),
                    BulkActionType.ResetToDefaults => RecommendedSettingsResolver.HasDefaultValue(setting),
                    _ => false
                };

                if (wouldChange)
                {
                    count++;
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"[BulkSettings] Error checking affected state for '{setting.Id}': {ex.Message}");
            }
        }

        return count;
    }

    private Task<List<SettingDefinition>> ResolveSettingsAsync(IEnumerable<string> settingIds)
    {
        var osInfo = RecommendedSettingsResolver.BuildOSInfo(versionService);

        var result = new List<SettingDefinition>();
        var idSet = settingIds.ToHashSet();

        foreach (var settingId in idSet)
        {
            try
            {
                var setting = settingsRegistry.GetById(settingId);
                if (setting == null)
                {
                    logService.Log(LogLevel.Warning, $"[BulkSettings] Setting '{settingId}' not found in registry");
                    continue;
                }

                if (RecommendedSettingsResolver.IsCompatibleWithCurrentOS(setting, osInfo))
                {
                    result.Add(setting);
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to resolve setting '{settingId}': {ex.Message}");
            }
        }

        return Task.FromResult(result);
    }
}
