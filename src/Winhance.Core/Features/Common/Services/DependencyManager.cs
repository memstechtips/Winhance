using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services
{
    /// <summary>
    /// Manages dependencies between settings, ensuring that dependent settings are properly handled
    /// when their required settings change state.
    /// </summary>
    public class DependencyManager : IDependencyManager
    {
        private readonly ILogService _logService;
        
        public DependencyManager(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }
        
        /// <summary>
        /// Handles the enabling of a setting by automatically enabling any required settings.
        /// </summary>
        /// <param name="settingId">The ID of the setting that is being enabled.</param>
        /// <param name="allSettings">All available settings that might be required by the enabled setting.</param>
        /// <returns>True if all required settings were enabled successfully; otherwise, false.</returns>
        public bool HandleSettingEnabled(string settingId, IEnumerable<ISettingItem> allSettings)
        {
            if (string.IsNullOrEmpty(settingId))
            {
                _logService.Log(LogLevel.Warning, "Cannot handle dependencies for null or empty setting ID");
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
                return true; // No dependencies, so nothing to enable
            }
            
            // Get unsatisfied dependencies
            var unsatisfiedDependencies = GetUnsatisfiedDependencies(settingId, allSettings);
            
            // Enable all dependencies
            return EnableDependencies(unsatisfiedDependencies);
        }
        
        /// <summary>
        /// Gets a list of unsatisfied dependencies for a setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <param name="allSettings">All available settings that might be dependencies.</param>
        /// <returns>A list of settings that are required by the specified setting but are not enabled.</returns>
        public List<ISettingItem> GetUnsatisfiedDependencies(string settingId, IEnumerable<ISettingItem> allSettings)
        {
            var unsatisfiedDependencies = new List<ISettingItem>();
            
            if (string.IsNullOrEmpty(settingId))
            {
                _logService.Log(LogLevel.Warning, "Cannot get dependencies for null or empty setting ID");
                return unsatisfiedDependencies;
            }
            
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId);
            if (setting == null)
            {
                _logService.Log(LogLevel.Warning, $"Setting with ID '{settingId}' not found");
                return unsatisfiedDependencies;
            }
            
            if (setting.Dependencies == null || !setting.Dependencies.Any())
            {
                return unsatisfiedDependencies; // No dependencies
            }
            
            // Find all settings that this setting depends on
            foreach (var dependency in setting.Dependencies)
            {
                if (dependency.DependencyType == SettingDependencyType.RequiresEnabled)
                {
                    var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                    if (requiredSetting != null && !requiredSetting.IsSelected)
                    {
                        unsatisfiedDependencies.Add(requiredSetting);
                    }
                }
            }
            
            return unsatisfiedDependencies;
        }
        
        /// <summary>
        /// Enables all dependencies in the provided list.
        /// </summary>
        /// <param name="dependencies">The dependencies to enable.</param>
        /// <returns>True if all dependencies were enabled successfully; otherwise, false.</returns>
        public bool EnableDependencies(IEnumerable<ISettingItem> dependencies)
        {
            bool allSucceeded = true;
            
            foreach (var dependency in dependencies)
            {
                _logService.Log(LogLevel.Info, $"Automatically enabling dependency: {dependency.Name}");
                
                // Enable the dependency
                dependency.IsUpdatingFromCode = true;
                try
                {
                    dependency.IsSelected = true;
                }
                finally
                {
                    dependency.IsUpdatingFromCode = false;
                }
                
                // Apply the setting
                dependency.ApplySettingCommand?.Execute(null);
                
                // Check if the setting was successfully enabled
                if (!dependency.IsSelected)
                {
                    _logService.Log(LogLevel.Warning, $"Failed to enable required setting '{dependency.Name}'");
                    allSucceeded = false;
                }
            }
            
            return allSucceeded;
        }
        
        /// <summary>
        /// Determines if a setting can be enabled based on its dependencies.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <param name="allSettings">All available settings that might be dependencies.</param>
        /// <returns>True if the setting can be enabled; otherwise, false.</returns>
        public bool CanEnableSetting(string settingId, IEnumerable<ISettingItem> allSettings)
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
                    var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                    if (requiredSetting != null && !requiredSetting.IsSelected)
                    {
                        _logService.Log(LogLevel.Warning, $"Cannot enable '{setting.Name}' because '{requiredSetting.Name}' is disabled");
                        return false;
                    }
                }
                else if (dependency.DependencyType == SettingDependencyType.RequiresDisabled)
                {
                    var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
                    if (requiredSetting != null && requiredSetting.IsSelected)
                    {
                        _logService.Log(LogLevel.Warning, $"Cannot enable '{setting.Name}' because '{requiredSetting.Name}' is enabled");
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Handles the disabling of a setting by automatically disabling any dependent settings.
        /// </summary>
        /// <param name="settingId">The ID of the setting that was disabled.</param>
        /// <param name="allSettings">All available settings that might depend on the disabled setting.</param>
        public void HandleSettingDisabled(string settingId, IEnumerable<ISettingItem> allSettings)
        {
            if (string.IsNullOrEmpty(settingId))
            {
                _logService.Log(LogLevel.Warning, "Cannot handle dependencies for null or empty setting ID");
                return;
            }
            
            // Find all settings that depend on this setting
            var dependentSettings = allSettings.Where(s => 
                s.Dependencies != null && 
                s.Dependencies.Any(d => d.RequiredSettingId == settingId && 
                                      d.DependencyType == SettingDependencyType.RequiresEnabled));
            
            foreach (var dependentSetting in dependentSettings)
            {
                if (dependentSetting.IsSelected)
                {
                    _logService.Log(LogLevel.Info, $"Automatically disabling '{dependentSetting.Name}' as '{settingId}' was disabled");
                    
                    // Disable the dependent setting
                    dependentSetting.IsUpdatingFromCode = true;
                    try
                    {
                        dependentSetting.IsSelected = false;
                    }
                    finally
                    {
                        dependentSetting.IsUpdatingFromCode = false;
                    }
                    
                    // Apply the change
                    dependentSetting.ApplySettingCommand?.Execute(null);
                    
                    // Recursively handle any settings that depend on this one
                    HandleSettingDisabled(dependentSetting.Id, allSettings);
                }
            }
        }
    }
}
