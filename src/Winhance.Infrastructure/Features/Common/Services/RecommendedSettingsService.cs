using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
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
        IWindowsRegistryService registryService,
        IComboBoxResolver comboBoxResolver,
        IWindowsVersionService versionService,
        ILogService logService,
        IEventBus eventBus) : IRecommendedSettingsService
    {
        public string DomainName => "RecommendedSettings";

        public async Task ApplyRecommendedSettingsAsync(string settingId)
        {
            try
            {
                var domainService = domainServiceRouter.GetDomainService(settingId);
                logService.Log(LogLevel.Info, $"[RecommendedSettings] Starting to apply recommended settings for domain '{domainService.DomainName}'");

                var recommendedSettings = await GetRecommendedSettingsAsync(settingId);
                var settingsList = recommendedSettings.ToList();

                logService.Log(LogLevel.Info, $"[RecommendedSettings] Found {settingsList.Count} recommended settings for domain '{domainService.DomainName}'");

                if (!settingsList.Any())
                {
                    logService.Log(LogLevel.Info, $"[RecommendedSettings] No recommended settings found for domain '{domainService.DomainName}'");
                    return;
                }

                foreach (var setting in settingsList)
                {
                    try
                    {
                        var recommendedValue = GetRecommendedValueForSetting(setting);
                        logService.Log(LogLevel.Debug, $"[RecommendedSettings] Applying recommended setting '{setting.Id}' with value '{recommendedValue}'");

                        if (setting.InputType == InputType.Toggle)
                        {
                            var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
                            bool enableValue = false;

                            if (registrySetting != null && recommendedValue != null)
                            {
                                enableValue = recommendedValue.Equals(registrySetting.EnabledValue);
                                logService.Log(LogLevel.Debug, $"[RecommendedSettings] Toggle '{setting.Id}': RecommendedValue={recommendedValue}, EnabledValue={registrySetting.EnabledValue}, DisabledValue={registrySetting.DisabledValue}, Enable={enableValue}");
                            }

                            ApplySettingDirectly(setting, enableValue, recommendedValue);
                            await Task.Delay(150);
                        }
                        else if (setting.InputType == InputType.Selection)
                        {
                            var recommendedOption = GetRecommendedOptionFromSetting(setting);
                            logService.Log(LogLevel.Debug, $"[RecommendedSettings] Selection '{setting.Id}': RecommendedOption='{recommendedOption}', RecommendedValue='{recommendedValue}'");

                            if (recommendedOption != null)
                            {
                                var registryValue = GetRegistryValueFromOptionName(setting, recommendedOption);
                                var comboBoxIndex = GetCorrectSelectionIndex(setting, recommendedOption, registryValue);
                                ApplySettingDirectly(setting, true, comboBoxIndex);
                                await Task.Delay(150);
                            }
                            else
                            {
                                ApplySettingDirectly(setting, true, recommendedValue);
                                await Task.Delay(150);
                            }
                        }
                        else
                        {
                            ApplySettingDirectly(setting, true, recommendedValue);
                            await Task.Delay(150);
                        }

                        logService.Log(LogLevel.Debug, $"[RecommendedSettings] Successfully applied recommended setting '{setting.Id}'");
                    }
                    catch (Exception ex)
                    {
                        logService.Log(LogLevel.Warning, $"[RecommendedSettings] Failed to apply recommended setting '{setting.Id}': {ex.Message}");
                    }
                }

                logService.Log(LogLevel.Info, $"[RecommendedSettings] Completed applying recommended settings for domain '{domainService.DomainName}'");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[RecommendedSettings] Error applying recommended settings: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<SettingDefinition>> GetRecommendedSettingsAsync(string settingId)
        {
            try
            {
                var domainService = domainServiceRouter.GetDomainService(settingId);
                logService.Log(LogLevel.Debug, $"[RecommendedSettings] Getting recommended settings for domain '{domainService.DomainName}'");

                var allSettings = await domainService.GetSettingsAsync();

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


        private static string? GetRecommendedOptionFromSetting(SettingDefinition setting)
        {
            var primaryRegistrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.IsPrimary);
            if (primaryRegistrySetting?.CustomProperties?.TryGetValue("RecommendedOption", out var recommendedOption) == true)
            {
                return recommendedOption?.ToString();
            }
            return null;
        }

        private static int? GetCorrectSelectionIndex(SettingDefinition setting, string optionName, int? desiredRegistryValue)
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

        private static int? GetRegistryValueFromOptionName(SettingDefinition setting, string optionName)
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

        private static object? GetRecommendedValueForSetting(SettingDefinition setting)
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

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            return await Task.FromResult(Enumerable.Empty<SettingDefinition>());
        }

        private void ApplySettingDirectly(SettingDefinition setting, bool enable, object? value)
        {
            if (setting.RegistrySettings?.Count > 0)
            {
                if (setting.InputType == InputType.Selection && value is int index)
                {
                    var specificValues = comboBoxResolver.ResolveIndexToRawValues(setting, index);
                    
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        if (specificValues.TryGetValue(registrySetting.ValueName, out var specificValue))
                        {
                            if (specificValue == null)
                            {
                                registryService.ApplySetting(registrySetting, false);
                            }
                            else
                            {
                                registryService.ApplySetting(registrySetting, true, Convert.ToInt32(specificValue));
                            }
                        }
                        else
                        {
                            bool applyValue = comboBoxResolver.GetValueFromIndex(setting, index) != 0;
                            registryService.ApplySetting(registrySetting, applyValue);
                        }
                    }
                }
                else if (setting.InputType == InputType.Toggle)
                {
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        registryService.ApplySetting(registrySetting, enable);
                    }
                }

                eventBus.Publish(new SettingAppliedEvent(setting.Id, enable, value));
                logService.Log(LogLevel.Debug, $"[RecommendedSettings] Published SettingAppliedEvent for '{setting.Id}'");
            }
        }
    }
}
