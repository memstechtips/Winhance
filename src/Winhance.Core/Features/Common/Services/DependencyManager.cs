using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services
{
    public class DependencyManager : IDependencyManager
    {
        private readonly ILogService _logService;
        private readonly IGlobalSettingsRegistry _globalSettingsRegistry;

        public DependencyManager(ILogService logService, IGlobalSettingsRegistry globalSettingsRegistry)
        {
            _logService = logService;
            _globalSettingsRegistry = globalSettingsRegistry;
        }

        public async Task<bool> HandleSettingEnabledAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ISystemSettingsDiscoveryService discoveryService)
        {
            var setting = FindSetting(settingId, allSettings);
            if (setting?.Dependencies == null || !setting.Dependencies.Any())
                return true;

            bool allSucceeded = true;
            foreach (var dependency in setting.Dependencies)
            {
                var requiredSetting = FindSetting(dependency.RequiredSettingId, allSettings);
                if (requiredSetting == null)
                {
                    _logService.Log(LogLevel.Error, $"Required dependency '{dependency.RequiredSettingId}' not found for '{settingId}'");
                    allSucceeded = false;
                    continue;
                }

                if (!await IsDependencySatisfiedAsync(dependency, discoveryService))
                {
                    await ApplyDependencyAsync(dependency, requiredSetting, settingApplicationService);
                }
            }

            return allSucceeded;
        }

        public async Task HandleSettingDisabledAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ISystemSettingsDiscoveryService discoveryService)
        {
            var dependentSettings = allSettings.Where(s =>
                s.Dependencies?.Any(d =>
                    d.RequiredSettingId == settingId &&
                    (d.DependencyType == SettingDependencyType.RequiresEnabled ||
                     d.DependencyType == SettingDependencyType.RequiresSpecificValue)) == true);

            foreach (var dependentSetting in dependentSettings)
            {
                var currentState = await GetSettingStateAsync(dependentSetting.Id, discoveryService);
                if (currentState.Success && currentState.IsEnabled)
                {
                    await settingApplicationService.ApplySettingAsync(dependentSetting.Id, false);
                    await HandleSettingDisabledAsync(dependentSetting.Id, allSettings, settingApplicationService, discoveryService);
                }
            }
        }

        public async Task HandleSettingValueChangedAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ISystemSettingsDiscoveryService discoveryService)
        {
            var dependentSettings = allSettings.Where(s =>
                s.Dependencies?.Any(d =>
                    d.RequiredSettingId == settingId &&
                    d.DependencyType == SettingDependencyType.RequiresSpecificValue) == true);

            foreach (var dependentSetting in dependentSettings)
            {
                var currentState = await GetSettingStateAsync(dependentSetting.Id, discoveryService);
                if (!currentState.Success || !currentState.IsEnabled)
                    continue;

                var dependency = dependentSetting.Dependencies.First(d =>
                    d.RequiredSettingId == settingId &&
                    d.DependencyType == SettingDependencyType.RequiresSpecificValue);

                if (!await IsDependencySatisfiedAsync(dependency, discoveryService))
                {
                    await settingApplicationService.ApplySettingAsync(dependentSetting.Id, false);
                    await HandleSettingDisabledAsync(dependentSetting.Id, allSettings, settingApplicationService, discoveryService);
                }
            }
        }

        private ISettingItem? FindSetting(string settingId, IEnumerable<ISettingItem> allSettings)
        {
            return allSettings.FirstOrDefault(s => s.Id == settingId) ??
                   _globalSettingsRegistry.GetSetting(settingId);
        }

        private async Task<SettingStateResult> GetSettingStateAsync(string settingId, ISystemSettingsDiscoveryService discoveryService)
        {
            var setting = _globalSettingsRegistry.GetSetting(settingId);
            if (setting == null)
                return new SettingStateResult { Success = false, ErrorMessage = $"Setting '{settingId}' not found" };

            if (setting is not SettingDefinition settingDefinition)
                return new SettingStateResult { Success = false, ErrorMessage = $"Setting '{settingId}' is not a SettingDefinition" };

            var results = await discoveryService.GetSettingStatesAsync(new[] { settingDefinition });
            return results.TryGetValue(settingId, out var result) ? result : new SettingStateResult { Success = false };
        }

        private async Task<bool> IsDependencySatisfiedAsync(SettingDependency dependency, ISystemSettingsDiscoveryService discoveryService)
        {
            var currentState = await GetSettingStateAsync(dependency.RequiredSettingId, discoveryService);
            if (!currentState.Success)
                return false;

            return dependency.DependencyType switch
            {
                SettingDependencyType.RequiresEnabled => currentState.IsEnabled,
                SettingDependencyType.RequiresDisabled => !currentState.IsEnabled,
                SettingDependencyType.RequiresSpecificValue => !string.IsNullOrEmpty(dependency.RequiredValue) &&
                    string.Equals(currentState.CurrentValue?.ToString(), dependency.RequiredValue, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }

        private async Task ApplyDependencyAsync(SettingDependency dependency, ISettingItem requiredSetting, ISettingApplicationService settingApplicationService)
        {
            try
            {
                if (dependency.DependencyType == SettingDependencyType.RequiresSpecificValue)
                {
                    if (requiredSetting.InputType == InputType.Selection && !string.IsNullOrEmpty(dependency.RequiredValue))
                    {
                        await settingApplicationService.ApplySettingAsync(dependency.RequiredSettingId, true, dependency.RequiredValue);
                    }
                    else
                    {
                        await settingApplicationService.ApplySettingAsync(dependency.RequiredSettingId, true);
                    }
                }
                else
                {
                    bool enableValue = dependency.DependencyType == SettingDependencyType.RequiresEnabled;
                    await settingApplicationService.ApplySettingAsync(dependency.RequiredSettingId, enableValue);
                }
            }
            catch (ArgumentException ex) when (ex.Message.Contains("not found"))
            {
                _logService.Log(LogLevel.Warning,
                    $"Cannot apply dependency '{dependency.RequiredSettingId}' - likely filtered due to OS/hardware compatibility. Skipping.");
            }
        }
    }
}