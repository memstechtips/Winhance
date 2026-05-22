using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class BulkSettingsActionService(
    ICompatibleSettingsRegistry settingsRegistry,
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

                if (setting.InputType == InputType.Toggle)
                {
                    var toggleState = SettingDefinitionToggleState.GetRecommendedToggleState(setting);
                    if (toggleState is not bool enableValue)
                    {
                        // No recommendation across any backing store — nothing to apply.
                        logService.Log(LogLevel.Debug, $"[BulkSettings] Skipping '{setting.Id}' - no recommended toggle state");
                        continue;
                    }

                    // Mirror per-card HandleToggleAsync: pass only SettingId + Enable.
                    // The apply pipeline derives the registry write from EnabledValue/DisabledValue.
                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = enableValue,
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
                    var valueToApply = GetRecommendedValueForSetting(setting)
                        ?? BuildPowerCfgApplyValue(setting, useRecommended: true);
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
                    var valueToApply = GetDefaultValueForSetting(setting)
                        ?? BuildPowerCfgApplyValue(setting, useRecommended: false);
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
                    BulkActionType.ApplyRecommended => HasRecommendedValue(setting),
                    BulkActionType.ResetToDefaults => HasDefaultValue(setting),
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
        var osInfo = new OSInfo
        {
            BuildNumber = versionService.GetWindowsBuildNumber(),
            IsWindows10 = !versionService.IsWindows11(),
            IsWindows11 = versionService.IsWindows11()
        };

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

                if (IsCompatibleWithCurrentOS(setting, osInfo))
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
        if (SettingDefinitionToggleState.GetRecommendedToggleState(setting).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsRecommended) == true) return true;
        return false;
    }

    private static bool HasDefaultValue(SettingDefinition setting)
    {
        if (SettingDefinitionToggleState.GetDefaultToggleState(setting).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsDefault) == true) return true;
        return false;
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
            // 1:1 — see UnitConversionHelper for the rationale.
            "milliseconds" => systemValue,
            _ => systemValue
        };
    }
}
