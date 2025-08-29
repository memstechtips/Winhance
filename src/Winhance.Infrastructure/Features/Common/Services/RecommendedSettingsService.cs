using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    internal class OSInfo
    {
        public int BuildNumber { get; set; }
        public bool IsWindows10 { get; set; }
        public bool IsWindows11 { get; set; }
    }
    public class RecommendedSettingsService : IRecommendedSettingsService
    {
        private readonly IDomainServiceRouter _domainServiceRouter;
        private readonly ISystemServices _systemServices;
        private readonly ILogService _logService;
        private readonly IEventBus _eventBus;

        public string DomainName => "RecommendedSettings";

        public RecommendedSettingsService(
            IDomainServiceRouter DomainServiceRouter,
            ISystemServices systemServices,
            ILogService logService,
            IEventBus eventBus
        )
        {
            _domainServiceRouter =
                DomainServiceRouter
                ?? throw new ArgumentNullException(nameof(DomainServiceRouter));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public async Task ApplyRecommendedSettingsAsync(string settingId)
        {
            try
            {
                // Get the domain service using the provided setting ID
                var domainService = _domainServiceRouter.GetDomainService(settingId);

                _logService.Log(
                    LogLevel.Info,
                    $"[RecommendedSettings] Starting to apply recommended settings for domain '{domainService.DomainName}'"
                );

                var recommendedSettings = await GetRecommendedSettingsAsync(settingId);
                var settingsList = recommendedSettings.ToList();

                _logService.Log(
                    LogLevel.Info,
                    $"[RecommendedSettings] Found {settingsList.Count} recommended settings for domain '{domainService.DomainName}'"
                );

                if (!settingsList.Any())
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"[RecommendedSettings] No recommended settings found for domain '{domainService.DomainName}'"
                    );
                    return;
                }

                foreach (var setting in settingsList)
                {
                    try
                    {
                        // Get recommended value from registry settings
                        var recommendedValue = GetRecommendedValueForSetting(setting);

                        _logService.Log(
                            LogLevel.Debug,
                            $"[RecommendedSettings] Applying recommended setting '{setting.Id}' with value '{recommendedValue}'"
                        );

                        // Handle different input types appropriately
                        if (setting.InputType == SettingInputType.Toggle)
                        {
                            bool enableValue = recommendedValue != null ? Convert.ToBoolean(recommendedValue) : true;
                            await domainService.ApplySettingAsync(setting.Id, enableValue, recommendedValue);
                            _eventBus.Publish(new SettingAppliedEvent(setting.Id, enableValue, recommendedValue));
                            await Task.Delay(150);
                        }
                        else if (setting.InputType == SettingInputType.Selection)
                        {
                            var recommendedOption = GetRecommendedOptionFromSetting(setting);
                            _logService.Log(
                                LogLevel.Debug,
                                $"[RecommendedSettings] Selection '{setting.Id}': RecommendedOption='{recommendedOption}', RecommendedValue='{recommendedValue}'"
                            );

                            if (recommendedOption != null)
                            {
                                var registryValue = GetRegistryValueFromOptionName(setting, recommendedOption);
                                var comboBoxIndex = GetCorrectSelectionIndex(setting, recommendedOption, registryValue);
                                await domainService.ApplySettingAsync(setting.Id, true, comboBoxIndex);
                                _eventBus.Publish(new SettingAppliedEvent(setting.Id, true, comboBoxIndex));
                                await Task.Delay(150);
                            }
                            else
                            {
                                await domainService.ApplySettingAsync(setting.Id, true, recommendedValue);
                                _eventBus.Publish(new SettingAppliedEvent(setting.Id, true, recommendedValue));
                                await Task.Delay(150);
                            }
                        }
                        else
                        {
                            await domainService.ApplySettingAsync(setting.Id, true, recommendedValue);
                            _eventBus.Publish(new SettingAppliedEvent(setting.Id, true, recommendedValue));
                            await Task.Delay(150);
                        }

                        _logService.Log(
                            LogLevel.Debug,
                            $"[RecommendedSettings] Successfully applied recommended setting '{setting.Id}'"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"[RecommendedSettings] Failed to apply recommended setting '{setting.Id}': {ex.Message}"
                        );
                        // Continue with other settings even if one fails
                    }
                }

                _logService.Log(
                    LogLevel.Info,
                    $"[RecommendedSettings] Completed applying recommended settings for domain '{domainService.DomainName}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[RecommendedSettings] Error applying recommended settings: {ex.Message}"
                );
                throw;
            }
        }

        public async Task<IEnumerable<SettingDefinition>> GetRecommendedSettingsAsync(
            string settingId
        )
        {
            try
            {
                // Get the domain service using the provided setting ID
                var domainService = _domainServiceRouter.GetDomainService(settingId);

                _logService.Log(
                    LogLevel.Debug,
                    $"[RecommendedSettings] Getting recommended settings for domain '{domainService.DomainName}'"
                );

                // Get all settings for the domain
                var allSettings = await domainService.GetSettingsAsync();

                // Get current OS version info for compatibility filtering
                var osInfo = new OSInfo
                {
                    BuildNumber = _systemServices.GetWindowsBuildNumber(),
                    IsWindows10 = !_systemServices.IsWindows11(),
                    IsWindows11 = _systemServices.IsWindows11()
                };

                // Filter settings that have RecommendedValue and are compatible with current OS
                var recommendedSettings = allSettings.Where(setting =>
                    HasRecommendedValue(setting) && IsCompatibleWithCurrentOS(setting, osInfo)
                );

                var settingsList = recommendedSettings.ToList();

                _logService.Log(
                    LogLevel.Debug,
                    $"[RecommendedSettings] Found {settingsList.Count} recommended settings for domain '{domainService.DomainName}'"
                );

                return settingsList;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[RecommendedSettings] Error getting recommended settings: {ex.Message}"
                );
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

        private static bool IsCompatibleWithCurrentOS(
            SettingDefinition setting,
            OSInfo osInfo
        )
        {
            // If it's a SettingDefinition, check OS compatibility
            if (setting is SettingDefinition customSetting)
            {
                // Check Windows version compatibility
                if (customSetting.IsWindows10Only && !osInfo.IsWindows10)
                {
                    return false;
                }

                if (customSetting.IsWindows11Only && !osInfo.IsWindows11)
                {
                    return false;
                }

                // Check build number range compatibility
                if (
                    customSetting.MinimumBuildNumber.HasValue
                    && osInfo.BuildNumber < customSetting.MinimumBuildNumber.Value
                )
                {
                    return false;
                }

                if (
                    customSetting.MaximumBuildNumber.HasValue
                    && osInfo.BuildNumber > customSetting.MaximumBuildNumber.Value
                )
                {
                    return false;
                }
            }

            return true;
        }

        // Required by IDomainService but not used for this service
        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            return await Task.FromResult(Enumerable.Empty<SettingDefinition>());
        }

        // Required by IDomainService but not used for this service
        public async Task<IEnumerable<SettingDefinition>> GetRawSettingsAsync()
        {
            return await Task.FromResult(Enumerable.Empty<SettingDefinition>());
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            await Task.CompletedTask;
            throw new NotSupportedException(
                "RecommendedSettingsService does not support direct setting application. Use ApplyRecommendedSettingsAsync instead."
            );
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            return await Task.FromResult(false);
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            return await Task.FromResult<object?>(null);
        }
    }
}
