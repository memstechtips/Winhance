using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class OSInfo
{
    public int BuildNumber { get; set; }
    public bool IsWindows10 { get; set; }
    public bool IsWindows11 { get; set; }
}

public class RecommendedSettingsService(
    IDomainServiceRouter domainServiceRouter,
    IWindowsVersionService versionService,
    ILogService logService) : IRecommendedSettingsService
{
    public string DomainName => "RecommendedSettings";

    public async Task<IEnumerable<SettingDefinition>> GetRecommendedSettingsAsync(string settingId)
    {
        try
        {
            var domainService = domainServiceRouter.GetDomainService(settingId);
            logService.Log(LogLevel.Debug, $"[RecommendedSettings] Getting recommended settings for domain '{domainService.DomainName}'");

            var allSettings = await domainService.GetSettingsAsync().ConfigureAwait(false);

            var osInfo = new OSInfo
            {
                BuildNumber = versionService.GetWindowsBuildNumber(),
                IsWindows10 = !versionService.IsWindows11(),
                IsWindows11 = versionService.IsWindows11()
            };

            var recommendedSettings = allSettings.Where(setting =>
                HasRecommendedValue(setting) && IsCompatibleWithCurrentOS(setting, osInfo)
            );

            var settingsList = recommendedSettings.ToList();
            logService.Log(LogLevel.Debug, $"[RecommendedSettings] Found {settingsList.Count} recommended settings for domain '{domainService.DomainName}'");

            return settingsList;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[RecommendedSettings] Error getting recommended settings: {ex.Message}");
            throw;
        }
    }


    internal static int? GetRecommendedSelectionIndex(SettingDefinition setting)
    {
        var options = setting.ComboBox?.Options;
        if (options == null) return null;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].IsRecommended) return i;
        }
        return null;
    }

    internal static object? GetRecommendedValueForSetting(SettingDefinition setting)
    {
        // Get the first registry setting that has a RecommendedValue
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
        return registrySetting?.RecommendedValue;
    }

    private static bool HasRecommendedValue(SettingDefinition setting)
    {
        return setting.RegistrySettings?.Any(rs => rs.RecommendedValue != null) == true;
    }

    private static bool IsCompatibleWithCurrentOS(SettingDefinition setting, OSInfo osInfo)
    {
        if (setting.IsWindows10Only && !osInfo.IsWindows10) return false;
        if (setting.IsWindows11Only && !osInfo.IsWindows11) return false;

        // Check SupportedBuildRanges (takes precedence over min/max if specified)
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

    public Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
    {
        return Task.FromResult(Enumerable.Empty<SettingDefinition>());
    }
}
