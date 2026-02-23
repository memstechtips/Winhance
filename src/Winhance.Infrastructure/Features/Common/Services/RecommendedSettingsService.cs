using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
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


        internal static string? GetRecommendedOptionFromSetting(SettingDefinition setting)
        {
            var primaryRegistrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.IsPrimary);
            if (primaryRegistrySetting?.CustomProperties?.TryGetValue("RecommendedOption", out var recommendedOption) == true)
            {
                return recommendedOption?.ToString();
            }
            return null;
        }

        internal static int? GetCorrectSelectionIndex(SettingDefinition setting, string optionName, int? desiredRegistryValue)
        {
            var primaryRegistrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.IsPrimary);
            if (primaryRegistrySetting?.CustomProperties?.TryGetValue("ComboBoxOptions", out var comboBoxOptionsObj) == true
                && comboBoxOptionsObj is Dictionary<string, int> comboBoxOptions)
            {
                // Create a list ordered by key name (alphabetical) to match GenericResolver logic
                var orderedOptions = comboBoxOptions.OrderBy(kvp => kvp.Key).ToList();

                // Find the index of our desired option in this ordered list
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

        internal static int? GetRegistryValueFromOptionName(SettingDefinition setting, string optionName)
        {
            var primaryRegistrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.IsPrimary);
            if (primaryRegistrySetting?.CustomProperties?.TryGetValue("ComboBoxOptions", out var comboBoxOptionsObj) == true
                && comboBoxOptionsObj is Dictionary<string, int> comboBoxOptions)
            {
                // Simply return the registry value for this option name
                if (comboBoxOptions.TryGetValue(optionName, out var registryValue))
                {
                    return registryValue;
                }
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
            if (setting.MinimumBuildNumber.HasValue && osInfo.BuildNumber < setting.MinimumBuildNumber.Value) return false;
            if (setting.MaximumBuildNumber.HasValue && osInfo.BuildNumber > setting.MaximumBuildNumber.Value) return false;
            return true;
        }

        public Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            return Task.FromResult(Enumerable.Empty<SettingDefinition>());
        }
    }
}
