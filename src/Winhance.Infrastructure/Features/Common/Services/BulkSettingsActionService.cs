using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class BulkSettingsActionService(
    IDomainServiceRouter domainServiceRouter,
    IWindowsVersionService versionService,
    ISettingApplicationService settingApplicationService,
    IProcessRestartManager processRestartManager,
    ILogService logService) : IBulkSettingsActionService
{
    public async Task<int> ApplyRecommendedAsync(
        IEnumerable<string> settingIds,
        IProgress<TaskProgressDetail>? progress = null)
    {
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        int applied = 0;
        int total = settings.Count;
        var appliedForRestart = new List<SettingDefinition>(total);

        // Suppress per-setting restarts during the loop — many settings share a
        // RestartProcess (e.g. "explorer") and restarting after each one kills the
        // shell repeatedly. We flush coalesced restarts once at the end.
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
                    StatusText = $"Applying recommended: {setting.Name}",
                    QueueCurrent = i + 1,
                    QueueTotal = total,
                    IsActive = true
                });

                var recommendedValue = GetRecommendedValueForSetting(setting);

                if (setting.InputType == InputType.Toggle)
                {
                    var toggleState = ResolveRecommendedToggleState(setting, recommendedValue);
                    if (toggleState is not bool enableValue)
                    {
                        // No recommendation across any backing store — nothing to apply.
                        logService.Log(LogLevel.Debug, $"[BulkSettings] Skipping '{setting.Id}' - no recommended toggle state");
                        continue;
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
                    var powerCfgValue = BuildPowerCfgApplyValue(setting, useRecommended: true);
                    if (powerCfgValue != null)
                    {
                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = true,
                            Value = powerCfgValue,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        var recommendedIndex = GetRecommendedIndex(setting);
                        if (recommendedIndex is int idx)
                        {
                            await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                            {
                                SettingId = setting.Id,
                                Enable = true,
                                Value = idx,
                                SkipValuePrerequisites = true
                            }).ConfigureAwait(false);
                        }
                        // No else: if no IsRecommended option (legitimate for informational ComboBoxes
                        // like "pick a DNS provider"), skip.
                    }
                }
                else
                {
                    var valueToApply = recommendedValue ?? BuildPowerCfgApplyValue(setting, useRecommended: true);
                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = true,
                        Value = valueToApply,
                        SkipValuePrerequisites = true
                    }).ConfigureAwait(false);
                }

                applied++;
                appliedForRestart.Add(setting);
                logService.Log(LogLevel.Debug, $"[BulkSettings] Applied recommended for '{setting.Id}'");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to apply recommended for '{setting.Id}': {ex.Message}");
            }
        }
        } // end SuppressRestarts scope

        await processRestartManager.FlushCoalescedRestartsAsync(appliedForRestart).ConfigureAwait(false);

        progress?.Report(new TaskProgressDetail
        {
            Progress = 100,
            StatusText = $"Applied {applied} of {total} settings",
            IsCompletion = true,
            IsActive = false
        });

        return applied;
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

                var defaultValue = GetDefaultValueForSetting(setting);

                if (setting.InputType == InputType.Toggle)
                {
                    var toggleState = ResolveDefaultToggleState(setting, defaultValue);
                    if (toggleState is not bool enableValue)
                    {
                        logService.Log(LogLevel.Debug, $"[BulkSettings] Skipping '{setting.Id}' - no default toggle state");
                        continue;
                    }

                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = enableValue,
                        Value = defaultValue,
                        ResetToDefault = true,
                        SkipValuePrerequisites = true
                    }).ConfigureAwait(false);
                }
                else if (setting.InputType == InputType.Selection)
                {
                    var powerCfgValue = BuildPowerCfgApplyValue(setting, useRecommended: false);
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
                        var defaultIndex = GetDefaultIndex(setting);
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
                    var valueToApply = defaultValue ?? BuildPowerCfgApplyValue(setting, useRecommended: false);
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
                bool wouldChange = actionType switch
                {
                    BulkActionType.ApplyRecommended => HasRecommendedValue(setting),
                    BulkActionType.ResetToDefaults => HasDefaultValue(setting) || HasGroupPolicySettings(setting),
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

    private async Task<List<SettingDefinition>> ResolveSettingsAsync(IEnumerable<string> settingIds)
    {
        var osInfo = new OSInfo
        {
            BuildNumber = versionService.GetWindowsBuildNumber(),
            IsWindows10 = !versionService.IsWindows11(),
            IsWindows11 = versionService.IsWindows11()
        };

        var result = new List<SettingDefinition>();
        // Cache domain settings lookups to avoid repeated calls for the same domain
        var domainSettingsCache = new Dictionary<string, List<SettingDefinition>>();
        var idSet = settingIds.ToHashSet();

        foreach (var settingId in idSet)
        {
            try
            {
                var domainService = domainServiceRouter.GetDomainService(settingId);
                var domainName = domainService.DomainName;

                if (!domainSettingsCache.TryGetValue(domainName, out var domainSettings))
                {
                    var allSettings = await domainService.GetSettingsAsync().ConfigureAwait(false);
                    domainSettings = allSettings.ToList();
                    domainSettingsCache[domainName] = domainSettings;
                }

                var setting = domainSettings.FirstOrDefault(s => s.Id == settingId);
                if (setting != null && IsCompatibleWithCurrentOS(setting, osInfo))
                {
                    result.Add(setting);
                }
                else if (setting == null)
                {
                    logService.Log(LogLevel.Warning, $"[BulkSettings] Setting '{settingId}' not found in domain '{domainName}'");
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to resolve setting '{settingId}': {ex.Message}");
            }
        }

        return result;
    }

    private static bool IsCompatibleWithCurrentOS(SettingDefinition setting, OSInfo osInfo)
    {
        if (setting.IsWindows10Only && !osInfo.IsWindows10) return false;
        if (setting.IsWindows11Only && !osInfo.IsWindows11) return false;

        if (setting.SupportedBuildRanges?.Count > 0)
        {
            bool inSupportedRange = setting.SupportedBuildRanges.Any(range =>
                osInfo.BuildNumber >= range.MinBuild && osInfo.BuildNumber <= range.MaxBuild);
            if (!inSupportedRange) return false;
        }
        else
        {
            if (setting.MinimumBuildNumber.HasValue && osInfo.BuildNumber < setting.MinimumBuildNumber.Value) return false;
            if (setting.MaximumBuildNumber.HasValue && osInfo.BuildNumber > setting.MaximumBuildNumber.Value) return false;
        }

        return true;
    }

    private static object? GetRecommendedValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
        return registrySetting?.RecommendedValue;
    }

    private static object? GetDefaultValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.DefaultValue != null);
        return registrySetting?.DefaultValue;
    }

    private static bool HasRecommendedValue(SettingDefinition setting)
    {
        if (ResolveRecommendedToggleState(setting, GetRecommendedValueForSetting(setting)).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsRecommended) == true) return true;
        return false;
    }

    private static bool HasDefaultValue(SettingDefinition setting)
    {
        if (ResolveDefaultToggleState(setting, GetDefaultValueForSetting(setting)).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsDefault) == true) return true;
        return false;
    }

    private static bool HasGroupPolicySettings(SettingDefinition setting)
    {
        return setting.RegistrySettings?.Any(rs => rs.IsGroupPolicy) == true;
    }

    private static int? GetRecommendedIndex(SettingDefinition setting)
    {
        var opts = setting.ComboBox?.Options;
        if (opts is null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (opts[i].IsRecommended) return i;
        return null;
    }

    private static int? GetDefaultIndex(SettingDefinition setting)
    {
        var opts = setting.ComboBox?.Options;
        if (opts is null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (opts[i].IsDefault) return i;
        return null;
    }

    // Resolves the target Enable flag for a Toggle setting under "Apply Recommended".
    // Priority matches SettingItemViewModel.ToggleRecommendedState so per-card and bulk
    // agree:
    //   1. SettingDefinition.RecommendedToggleState (explicit override — wins over any
    //      per-key RecommendedValue, e.g. when a primary key's recommendation is "no
    //      change" but a secondary key has a value for a different reason).
    //   2. RegistrySetting with non-null RecommendedValue → derive from EnabledValue match
    //   3. First ScheduledTaskSetting with RecommendedState set → use directly
    //   4. Null → caller should skip (no recommendation exists)
    private static bool? ResolveRecommendedToggleState(SettingDefinition setting, object? recommendedRegistryValue)
    {
        if (setting.RecommendedToggleState is bool explicitState)
            return explicitState;

        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
        if (registrySetting != null && recommendedRegistryValue != null)
        {
            return registrySetting.EnabledValue?.Any(ev => ev != null && recommendedRegistryValue.Equals(ev)) == true;
        }

        var taskSetting = setting.ScheduledTaskSettings?.FirstOrDefault(ts => ts.RecommendedState.HasValue);
        if (taskSetting?.RecommendedState is bool taskState)
            return taskState;

        return null;
    }

    // Resolves the target Enable flag for a Toggle setting under "Reset to Default".
    //   1. RegistrySetting with DefaultValue → derive from EnabledValue match
    //   2. RegistrySetting that is a group policy → disabled (key-absent)
    //   3. Key-absent default: DefaultValue null AND EnabledValue/DisabledValue carries a
    //      null sentinel — mirrors SettingItemViewModel.ToggleDefaultState so Quick Actions
    //      resets settings like start-power-lock-option the same way the per-card button does.
    //   4. First ScheduledTaskSetting with DefaultState set → use directly
    //   5. Null → caller should skip
    private static bool? ResolveDefaultToggleState(SettingDefinition setting, object? defaultRegistryValue)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.DefaultValue != null || rs.IsGroupPolicy);
        if (registrySetting != null)
        {
            if (defaultRegistryValue != null)
                return registrySetting.EnabledValue?.Any(ev => ev != null && defaultRegistryValue.Equals(ev)) == true;
            if (registrySetting.IsGroupPolicy)
                return false;
        }

        var primaryReg = setting.RegistrySettings?.FirstOrDefault(r => r.IsPrimary)
                      ?? setting.RegistrySettings?.FirstOrDefault();
        if (primaryReg != null && primaryReg.DefaultValue == null)
        {
            if (primaryReg.EnabledValue?.Any(ev => ev == null) == true) return true;
            if (primaryReg.DisabledValue?.Any(dv => dv == null) == true) return false;
        }

        var taskSetting = setting.ScheduledTaskSettings?.FirstOrDefault(ts => ts.DefaultState.HasValue);
        if (taskSetting?.DefaultState is bool taskState)
            return taskState;

        return null;
    }

    // For PowerCfg-backed Selection/NumericRange settings, build the value shape that
    // SettingApplicationService → PowerCfgApplier expects (matches what SettingItemViewModel
    // sends for AC/DC quick-set buttons). Returns null if the setting isn't PowerCfg-backed
    // or if neither AC nor DC has a target value.
    private static object? BuildPowerCfgApplyValue(SettingDefinition setting, bool useRecommended)
    {
        var pcfg = setting.PowerCfgSettings?.FirstOrDefault();
        if (pcfg == null) return null;

        int? acRaw = useRecommended ? pcfg.RecommendedValueAC : pcfg.DefaultValueAC;
        int? dcRaw = useRecommended ? pcfg.RecommendedValueDC : pcfg.DefaultValueDC;
        if (!acRaw.HasValue && !dcRaw.HasValue) return null;

        bool isSeparate = pcfg.PowerModeSupport == PowerModeSupport.Separate;

        if (setting.InputType == InputType.Selection)
        {
            int? acIdx = FindOptionIndexForPowerCfgValue(setting, acRaw);
            int? dcIdx = FindOptionIndexForPowerCfgValue(setting, dcRaw);

            if (isSeparate)
            {
                if (!acIdx.HasValue && !dcIdx.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acIdx ?? 0,
                    ["DCValue"] = dcIdx ?? 0
                };
            }
            return (object?)(acIdx ?? dcIdx);
        }

        if (setting.InputType == InputType.NumericRange)
        {
            // Stored values are system units (e.g. Seconds). PowerCfgApplier converts
            // display→system on its end, so we hand it display units here.
            string displayUnits = GetPowerCfgDisplayUnits(setting);
            int? acDisplay = acRaw.HasValue ? ConvertSystemToDisplayUnits(acRaw.Value, displayUnits) : null;
            int? dcDisplay = dcRaw.HasValue ? ConvertSystemToDisplayUnits(dcRaw.Value, displayUnits) : null;

            if (isSeparate)
            {
                if (!acDisplay.HasValue && !dcDisplay.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acDisplay ?? 0,
                    ["DCValue"] = dcDisplay ?? 0
                };
            }
            return (object?)(acDisplay ?? dcDisplay);
        }

        return null;
    }

    private static int? FindOptionIndexForPowerCfgValue(SettingDefinition setting, int? targetValue)
    {
        if (!targetValue.HasValue) return null;
        var opts = setting.ComboBox?.Options;
        if (opts == null) return null;
        for (int i = 0; i < opts.Count; i++)
        {
            if (opts[i].ValueMappings is { } m && m.TryGetValue("PowerCfgValue", out var v) && v != null)
            {
                try { if (Convert.ToInt32(v) == targetValue.Value) return i; }
                catch { }
            }
        }
        return null;
    }

    private static string GetPowerCfgDisplayUnits(SettingDefinition setting)
    {
        if (setting.NumericRange?.Units is { } unitsStr) return unitsStr;
        return setting.PowerCfgSettings?[0]?.Units ?? string.Empty;
    }

    // Inverse of PowerCfgApplier.ConvertToSystemUnits.
    private static int ConvertSystemToDisplayUnits(int systemValue, string? units)
    {
        return units?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            "milliseconds" => systemValue * 1000,
            _ => systemValue
        };
    }
}
