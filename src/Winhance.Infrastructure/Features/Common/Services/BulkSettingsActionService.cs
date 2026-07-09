using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Common.Services;

public class BulkSettingsActionService(
    ICatalogSettingsRegistry settingsRegistry,
    IWindowsVersionService versionService,
    ISettingApplicationService settingApplicationService,
    IProcessRestartManager processRestartManager,
    IRecommendedSettingsApplier recommendedSettingsApplier,
    ILogService logService,
    IChangeHistoryService changeHistory,
    ILocalizationService localizationService) : IBulkSettingsActionService
{
    public async Task<int> ApplyRecommendedAsync(
        IEnumerable<string> settingIds,
        IProgress<TaskProgressDetail>? progress = null)
    {
        using var changeBatch = changeHistory.BeginBatch(localizationService.GetString("QuickActions_ApplyRecommended"));
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
        using var changeBatch = changeHistory.BeginBatch(localizationService.GetString("QuickActions_ResetDefaults"));
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        var currentBuild = new WinBuild(versionService.GetWindowsBuildNumber(), versionService.GetWindowsBuildRevision());
        int applied = 0;
        int total = settings.Count;
        var appliedForRestart = new List<Setting>(total);

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
                    StatusText = $"Resetting to default: {setting.Display.Name}",
                    QueueCurrent = i + 1,
                    QueueTotal = total,
                    IsActive = true
                });

                if (setting.Control == ControlKind.Toggle)
                {
                    // Slice 3b: setting IS the catalog Setting; the build-aware reset engine
                    // (ApplyRequestResolver) resolves the per-OS default from its WindowsDefault-roled state,
                    // and the bulk loop only gates on whether a default EXISTS on this build. GetDefault is
                    // build-aware; no re-pairing needed.
                    if (CatalogToggleState.GetDefault(setting, currentBuild) is not bool enableValue)
                    {
                        logService.Log(LogLevel.Debug, $"[BulkSettings] Skipping '{setting.Id}' - no default toggle state");
                        continue;
                    }

                    // Mirror per-card HandleToggleAsync: pass only SettingId + Enable + ResetToDefault.
                    // The build-aware apply pipeline derives the per-OS registry write from the WindowsDefault state.
                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = enableValue,
                        ResetToDefault = true,
                        SkipValuePrerequisites = true
                    }).ConfigureAwait(false);
                }
                else if (setting.Control == ControlKind.Selection)
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
                else if (setting.Control == ControlKind.Slider)
                {
                    // Slider (NumericRange) reset. Population here is powercfg NumericRange only:
                    // GetDefaultValueForSetting was dead-in-effect (registry-free) and has no catalog overload,
                    // so it collapses to BuildPowerCfgApplyValue. GATED to Slider so power-plan-selection - whose
                    // DERIVED Control is PowerPlan (OptionSource), not the old InputType.Selection - is NOT reset
                    // here: the old code matched it in the Selection branch and skipped it (no static default),
                    // and it is likewise excluded from the affected-count (HasDefaultValue is false for PowerPlan).
                    var valueToApply = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: false);
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
        var currentBuild = new WinBuild(versionService.GetWindowsBuildNumber(), versionService.GetWindowsBuildRevision());
        int count = 0;

        foreach (var setting in settings)
        {
            try
            {
                // Counter must agree with the apply path. HasRecommendedValue / HasDefaultValue
                // resolve the toggle contribution from the catalog (build-aware), the same source the
                // reset/recommended loops now use, so the count never reports a setting the loop would skip.
                bool wouldChange = actionType switch
                {
                    BulkActionType.ApplyRecommended => RecommendedSettingsResolver.HasRecommendedValue(setting, currentBuild),
                    BulkActionType.ResetToDefaults => RecommendedSettingsResolver.HasDefaultValue(setting, currentBuild),
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

    private Task<List<Setting>> ResolveSettingsAsync(IEnumerable<string> settingIds)
    {
        var result = new List<Setting>();
        var idSet = settingIds.ToHashSet();

        foreach (var settingId in idSet)
        {
            try
            {
                // The catalog registry is current-OS scoped (OS build + hardware + existence) and
                // alias-normalizes the id, so an OS-incompatible setting returns null here - the old
                // BuildOSInfo + IsCompatibleWithCurrentOS re-filter is obviated.
                var setting = settingsRegistry.GetById(settingId);
                if (setting == null)
                {
                    logService.Log(LogLevel.Warning, $"[BulkSettings] Setting '{settingId}' not found in registry");
                    continue;
                }

                // Stateless one-shot Actions have no default/recommended STATE, so they are excluded from the bulk
                // "Reset to Defaults" / "Apply Recommended" ops (and their affected-count). Their old-executor reset
                // was an incoherent else-branch accident; a one-shot action is not a stateful setting to bulk-reset
                // or bulk-recommend. This also makes the resolver's Action-reset path unreachable (Marco 2026-07-03).
                if (setting.Control == ControlKind.Action)
                {
                    continue;
                }

                result.Add(setting);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to resolve setting '{settingId}': {ex.Message}");
            }
        }

        return Task.FromResult(result);
    }
}
