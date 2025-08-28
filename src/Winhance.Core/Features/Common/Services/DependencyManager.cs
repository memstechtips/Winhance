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

        public async Task<bool> HandleSettingEnabledAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService)
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

                if (!await IsDependencySatisfiedAsync(dependency, settingApplicationService))
                {
                    await ApplyDependencyAsync(dependency, requiredSetting, settingApplicationService);
                }
            }

            return allSucceeded;
        }

        public async Task HandleSettingDisabledAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService)
        {
            var dependentSettings = allSettings.Where(s =>
                s.Dependencies?.Any(d =>
                    d.RequiredSettingId == settingId &&
                    (d.DependencyType == SettingDependencyType.RequiresEnabled ||
                     d.DependencyType == SettingDependencyType.RequiresSpecificValue)) == true);

            foreach (var dependentSetting in dependentSettings)
            {
                var currentState = await settingApplicationService.GetSettingStateAsync(dependentSetting.Id);
                if (currentState.Success && currentState.IsEnabled)
                {
                    await settingApplicationService.ApplySettingAsync(dependentSetting.Id, false);
                    await HandleSettingDisabledAsync(dependentSetting.Id, allSettings, settingApplicationService);
                }
            }
        }

        public async Task HandleSettingValueChangedAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService)
        {
            var dependentSettings = allSettings.Where(s =>
                s.Dependencies?.Any(d =>
                    d.RequiredSettingId == settingId &&
                    d.DependencyType == SettingDependencyType.RequiresSpecificValue) == true);

            foreach (var dependentSetting in dependentSettings)
            {
                var currentState = await settingApplicationService.GetSettingStateAsync(dependentSetting.Id);
                if (!currentState.Success || !currentState.IsEnabled)
                    continue;

                var dependency = dependentSetting.Dependencies.First(d =>
                    d.RequiredSettingId == settingId &&
                    d.DependencyType == SettingDependencyType.RequiresSpecificValue);

                if (!await IsDependencySatisfiedAsync(dependency, settingApplicationService))
                {
                    await settingApplicationService.ApplySettingAsync(dependentSetting.Id, false);
                    await HandleSettingDisabledAsync(dependentSetting.Id, allSettings, settingApplicationService);
                }
            }
        }

        private ISettingItem? FindSetting(string settingId, IEnumerable<ISettingItem> allSettings)
        {
            return allSettings.FirstOrDefault(s => s.Id == settingId) ??
                   _globalSettingsRegistry.GetSetting(settingId);
        }

        private async Task<bool> IsDependencySatisfiedAsync(SettingDependency dependency, ISettingApplicationService settingApplicationService)
        {
            var currentState = await settingApplicationService.GetSettingStateAsync(dependency.RequiredSettingId);
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
            if (dependency.DependencyType == SettingDependencyType.RequiresSpecificValue)
            {
                if (requiredSetting.InputType == SettingInputType.Selection && !string.IsNullOrEmpty(dependency.RequiredValue))
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
    }
}