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
    ILogService logService) : IBulkSettingsActionService
{
    public async Task<int> ApplyRecommendedAsync(
        IEnumerable<string> settingIds,
        IProgress<TaskProgressDetail>? progress = null)
    {
        var settings = await ResolveSettingsAsync(settingIds).ConfigureAwait(false);
        int applied = 0;
        int total = settings.Count;

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
                    var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
                    bool enableValue = false;

                    if (registrySetting != null && recommendedValue != null)
                    {
                        enableValue = registrySetting.EnabledValue?.Any(ev => ev != null && recommendedValue.Equals(ev)) == true;
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
                    var recommendedOption = GetRecommendedOptionFromSetting(setting);

                    if (recommendedOption != null)
                    {
                        var registryValue = GetRegistryValueFromOptionName(setting, recommendedOption);
                        var comboBoxIndex = GetCorrectSelectionIndex(setting, recommendedOption, registryValue);
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

                applied++;
                logService.Log(LogLevel.Debug, $"[BulkSettings] Applied recommended for '{setting.Id}'");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to apply recommended for '{setting.Id}': {ex.Message}");
            }
        }

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
                    var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.DefaultValue != null || rs.IsGroupPolicy);
                    bool enableValue = false;

                    if (registrySetting != null && defaultValue != null)
                    {
                        enableValue = registrySetting.EnabledValue?.Any(ev => ev != null && defaultValue.Equals(ev)) == true;
                    }
                    // For group policy keys where DefaultValue is null, disable the setting
                    // (defaultValue is already null, enableValue stays false)

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
                    var defaultOption = GetDefaultOptionFromSetting(setting);

                    if (defaultOption != null)
                    {
                        var registryValue = GetRegistryValueFromOptionName(setting, defaultOption);
                        var comboBoxIndex = GetCorrectSelectionIndex(setting, defaultOption, registryValue);
                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = true,
                            Value = comboBoxIndex,
                            ResetToDefault = true,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                        {
                            SettingId = setting.Id,
                            Enable = defaultValue != null,
                            Value = defaultValue,
                            ResetToDefault = true,
                            SkipValuePrerequisites = true
                        }).ConfigureAwait(false);
                    }
                }
                else
                {
                    await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
                    {
                        SettingId = setting.Id,
                        Enable = defaultValue != null,
                        Value = defaultValue,
                        ResetToDefault = true,
                        SkipValuePrerequisites = true
                    }).ConfigureAwait(false);
                }

                applied++;
                logService.Log(LogLevel.Debug, $"[BulkSettings] Reset to default for '{setting.Id}'");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[BulkSettings] Failed to reset default for '{setting.Id}': {ex.Message}");
            }
        }

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
        return setting.RegistrySettings?.Any(rs => rs.RecommendedValue != null) == true;
    }

    private static bool HasDefaultValue(SettingDefinition setting)
    {
        return setting.RegistrySettings?.Any(rs => rs.DefaultValue != null) == true;
    }

    private static bool HasGroupPolicySettings(SettingDefinition setting)
    {
        return setting.RegistrySettings?.Any(rs => rs.IsGroupPolicy) == true;
    }

    private static string? GetRecommendedOptionFromSetting(SettingDefinition setting)
    {
        var primaryRegistrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.IsPrimary);
        return primaryRegistrySetting?.RecommendedOption;
    }

    private static string? GetDefaultOptionFromSetting(SettingDefinition setting)
    {
        var primaryRegistrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.IsPrimary);
        return primaryRegistrySetting?.DefaultOption;
    }

    private static int? GetRegistryValueFromOptionName(SettingDefinition setting, string optionName)
    {
        var primaryRegistrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.IsPrimary);
        if (primaryRegistrySetting?.ComboBoxOptions is { } comboBoxOptions)
        {
            if (comboBoxOptions.TryGetValue(optionName, out var registryValue))
            {
                return registryValue;
            }
        }
        return null;
    }

    private static int? GetCorrectSelectionIndex(SettingDefinition setting, string optionName, int? desiredRegistryValue)
    {
        var primaryRegistrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.IsPrimary);
        if (primaryRegistrySetting?.ComboBoxOptions is { } comboBoxOptions)
        {
            var orderedOptions = comboBoxOptions.OrderBy(kvp => kvp.Key).ToList();

            for (int i = 0; i < orderedOptions.Count; i++)
            {
                if (orderedOptions[i].Key == optionName && orderedOptions[i].Value == desiredRegistryValue)
                {
                    return i;
                }
            }
        }
        return null;
    }
}
