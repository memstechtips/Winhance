using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers; // RecommendedSettingsResolver

namespace Winhance.Infrastructure.Features.Common.Services;

public class RecommendedSettingsApplier(
    ICompatibleSettingsRegistry compatibleSettingsRegistry,
    IWindowsVersionService versionService,
    IProcessRestartManager processRestartManager,
    ILogService logService) : IRecommendedSettingsApplier
{
    public async Task<IReadOnlyList<SettingDefinition>> ApplyRecommendedToSettingsAsync(
        IReadOnlyList<SettingDefinition> settings,
        ISettingApplicationService apply,
        IProgress<TaskProgressDetail>? progress = null)
    {
        var appliedForRestart = new List<SettingDefinition>(settings.Count);
        int total = settings.Count;
        var currentBuild = new WinBuild(versionService.GetWindowsBuildNumber(), versionService.GetWindowsBuildRevision());

        // Suppress per-setting restarts; the CALLER flushes the coalesced restart.
        using (processRestartManager.SuppressRestarts())
        {
            for (int i = 0; i < total; i++)
            {
                var setting = settings[i];

                // A one-shot Action is not a stateful setting to bulk-recommend (mirrors
                // BulkSettingsActionService's reset-loop exclusion; Marco 2026-07-03 / 2026-07-08).
                // Skipping it here also keeps the else-branch value helpers (GetRecommendedValueForSetting /
                // GetDefaultValueForSetting) from ever seeing an Action - the catalog Action model drops the
                // RecommendedValue signal, so those helpers cannot be reproduced off the catalog.
                if (setting.InputType == InputType.Action)
                    continue;

                try
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = (double)i / total * 100,
                        StatusText = $"Applying recommended: {setting.Name}",
                        QueueCurrent = i + 1,
                        QueueTotal = total,
                        IsActive = true
                    });

                    if (setting.InputType == InputType.Toggle)
                    {
                        // Slice C: recommended toggle state comes from the catalog Recommended role (unconditional),
                        // == the old SettingDefinitionToggleState.GetRecommendedToggleState for every paired setting
                        // (RecommendedToggleStateConformanceTests); Find alias-normalizes the -win10 ids.
                        var paired = SettingCatalog.Find(setting.Id);
                        if (paired is null || CatalogToggleState.GetRecommended(paired, currentBuild) is not bool enableValue) continue; // no recommendation
                        await apply.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id, Enable = enableValue, SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }
                    else if (setting.InputType == InputType.Selection)
                    {
                        var powerCfgValue = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: true);
                        if (powerCfgValue != null)
                        {
                            await apply.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = setting.Id, Enable = true, Value = powerCfgValue, SkipValuePrerequisites = true
                            }).ConfigureAwait(false);
                        }
                        else
                        {
                            var idx = RecommendedSettingsResolver.GetRecommendedIndex(setting);
                            if (idx is not int recommendedIndex) continue; // no IsRecommended option
                            await apply.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = setting.Id, Enable = true, Value = recommendedIndex, SkipValuePrerequisites = true
                            }).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        var valueToApply = RecommendedSettingsResolver.GetRecommendedValueForSetting(setting)
                            ?? RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: true);
                        if (valueToApply == null) continue; // nothing recommended
                        await apply.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id, Enable = true, Value = valueToApply, SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }

                    appliedForRestart.Add(setting);
                    logService.Log(LogLevel.Debug, $"[RecommendedSettingsApplier] Applied recommended for '{setting.Id}'");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[RecommendedSettingsApplier] Failed to apply recommended for '{setting.Id}': {ex.Message}");
                }
            }
        }

        return appliedForRestart;
    }

    public async Task<IReadOnlyList<SettingDefinition>> ApplyRecommendedForFeatureAsync(
        string triggerSettingId, ISettingApplicationService apply)
    {
        var featureId = compatibleSettingsRegistry.GetFeatureIdForSetting(triggerSettingId)
            ?? throw new InvalidOperationException($"Setting '{triggerSettingId}' has no feature mapping");

        var osInfo = RecommendedSettingsResolver.BuildOSInfo(versionService);
        var settings = compatibleSettingsRegistry.GetFilteredSettings(featureId)
            .Where(s => s.Id != triggerSettingId && RecommendedSettingsResolver.IsCompatibleWithCurrentOS(s, osInfo))
            .ToList();

        logService.Log(LogLevel.Info, $"[RecommendedSettingsApplier] Applying recommended for feature '{featureId}' ({settings.Count} candidate settings)");
        return await ApplyRecommendedToSettingsAsync(settings, apply, null).ConfigureAwait(false);
    }

    public async Task ApplyRecommendedSettingsForFeatureAsync(string settingId, ISettingApplicationService apply)
    {
        var applied = await ApplyRecommendedForFeatureAsync(settingId, apply).ConfigureAwait(false);
        await processRestartManager.FlushCoalescedRestartsAsync(applied).ConfigureAwait(false);
    }
}
