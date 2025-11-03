using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services
{
    /// <summary>
    /// Service for managing dependencies between domain settings (SettingDefinition objects).
    /// This handles business logic dependencies at the domain layer.
    /// </summary>
    public class DomainDependencyService : IDomainDependencyService
    {
        private readonly ILogService _logService;

        public DomainDependencyService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public bool CanEnableSetting(string settingId, IEnumerable<SettingDefinition> allSettings, Dictionary<string, bool> currentSettingsState)
        {
            if (string.IsNullOrEmpty(settingId))
            {
                _logService.Log(LogLevel.Warning, "Cannot check dependencies for null or empty setting ID");
                return false;
            }

            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);
            if (setting == null)
            {
                _logService.Log(LogLevel.Warning, $"Setting with ID '{settingId}' not found");
                return false;
            }

            if (setting.Dependencies == null || !setting.Dependencies.Any())
            {
                return true; // No dependencies, so it can be enabled
            }

            foreach (var dependency in setting.Dependencies)
            {
                if (dependency.DependencyType == SettingDependencyType.RequiresEnabled)
                {
                    if (!currentSettingsState.TryGetValue(dependency.RequiredSettingId, out var isEnabled) || !isEnabled)
                    {
                        var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                        _logService.Log(LogLevel.Warning,
                            $"Cannot enable '{setting.Name}' because required setting '{requiredSetting?.Name ?? dependency.RequiredSettingId}' is disabled");
                        return false;
                    }
                }
                else if (dependency.DependencyType == SettingDependencyType.RequiresDisabled)
                {
                    if (currentSettingsState.TryGetValue(dependency.RequiredSettingId, out var isEnabled) && isEnabled)
                    {
                        var conflictingSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                        _logService.Log(LogLevel.Warning,
                            $"Cannot enable '{setting.Name}' because conflicting setting '{conflictingSetting?.Name ?? dependency.RequiredSettingId}' is enabled");
                        return false;
                    }
                }
            }

            return true;
        }

        public IEnumerable<string> GetRequiredDependencies(string settingId, IEnumerable<SettingDefinition> allSettings)
        {
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);
            if (setting?.Dependencies == null)
            {
                return Enumerable.Empty<string>();
            }

            return setting.Dependencies
                .Where(d => d.DependencyType == SettingDependencyType.RequiresEnabled)
                .Select(d => d.RequiredSettingId)
                .ToList();
        }

        public IEnumerable<string> GetConflictingDependencies(string settingId, IEnumerable<SettingDefinition> allSettings)
        {
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);
            if (setting?.Dependencies == null)
            {
                return Enumerable.Empty<string>();
            }

            return setting.Dependencies
                .Where(d => d.DependencyType == SettingDependencyType.RequiresDisabled)
                .Select(d => d.RequiredSettingId)
                .ToList();
        }

        public IEnumerable<string> GetDependentSettings(string settingId, IEnumerable<SettingDefinition> allSettings)
        {
            return allSettings
                .Where(s => s.Dependencies?.Any(d => d.RequiredSettingId == settingId && d.DependencyType == SettingDependencyType.RequiresEnabled) == true)
                .Select(s => s.Id)
                .ToList();
        }

        public Dictionary<string, string> ValidateSettingsDependencies(IEnumerable<SettingDefinition> allSettings, Dictionary<string, bool> currentSettingsState)
        {
            var validationErrors = new Dictionary<string, string>();

            foreach (var setting in allSettings)
            {
                if (!currentSettingsState.TryGetValue(setting.Id, out var isEnabled) || !isEnabled)
                {
                    continue; // Skip disabled settings
                }

                if (setting.Dependencies == null || !setting.Dependencies.Any())
                {
                    continue; // No dependencies to validate
                }

                foreach (var dependency in setting.Dependencies)
                {
                    if (dependency.DependencyType == SettingDependencyType.RequiresEnabled)
                    {
                        if (!currentSettingsState.TryGetValue(dependency.RequiredSettingId, out var requiredIsEnabled) || !requiredIsEnabled)
                        {
                            var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                            validationErrors[setting.Id] = $"Setting '{setting.Name}' requires '{requiredSetting?.Name ?? dependency.RequiredSettingId}' to be enabled";
                        }
                    }
                    else if (dependency.DependencyType == SettingDependencyType.RequiresDisabled)
                    {
                        if (currentSettingsState.TryGetValue(dependency.RequiredSettingId, out var conflictingIsEnabled) && conflictingIsEnabled)
                        {
                            var conflictingSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                            validationErrors[setting.Id] = $"Setting '{setting.Name}' conflicts with '{conflictingSetting?.Name ?? dependency.RequiredSettingId}' which is currently enabled";
                        }
                    }
                }
            }

            return validationErrors;
        }

        public Dictionary<string, bool> GetDependencyResolutionPlan(string settingId, IEnumerable<SettingDefinition> allSettings, Dictionary<string, bool> currentSettingsState)
        {
            var resolutionPlan = new Dictionary<string, bool>();
            var settingsToProcess = new Queue<string>();
            var processedSettings = new HashSet<string>();

            settingsToProcess.Enqueue(settingId);

            while (settingsToProcess.Count > 0)
            {
                var currentSettingId = settingsToProcess.Dequeue();

                if (processedSettings.Contains(currentSettingId))
                {
                    continue; // Already processed
                }

                processedSettings.Add(currentSettingId);

                var setting = allSettings.FirstOrDefault(s => s.Id == currentSettingId);
                if (setting?.Dependencies == null)
                {
                    continue;
                }

                foreach (var dependency in setting.Dependencies)
                {
                    if (dependency.DependencyType == SettingDependencyType.RequiresEnabled)
                    {
                        if (!currentSettingsState.TryGetValue(dependency.RequiredSettingId, out var isEnabled) || !isEnabled)
                        {
                            // Need to enable this dependency
                            resolutionPlan[dependency.RequiredSettingId] = true;
                            settingsToProcess.Enqueue(dependency.RequiredSettingId);
                        }
                    }
                    else if (dependency.DependencyType == SettingDependencyType.RequiresDisabled)
                    {
                        if (currentSettingsState.TryGetValue(dependency.RequiredSettingId, out var isEnabled) && isEnabled)
                        {
                            // Need to disable this conflicting setting
                            resolutionPlan[dependency.RequiredSettingId] = false;

                            // Also need to disable any settings that depend on this one
                            var dependentSettings = GetDependentSettings(dependency.RequiredSettingId, allSettings);
                            foreach (var dependentSettingId in dependentSettings)
                            {
                                if (currentSettingsState.TryGetValue(dependentSettingId, out var dependentIsEnabled) && dependentIsEnabled)
                                {
                                    resolutionPlan[dependentSettingId] = false;
                                }
                            }
                        }
                    }
                }
            }

            // Ensure the target setting is enabled in the plan
            resolutionPlan[settingId] = true;

            return resolutionPlan;
        }
    }
}
