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
    /// when their required settings change state. Supports cross-module dependencies and ComboBox value dependencies.
    /// </summary>
    public class DependencyManager : IDependencyManager
    {
        private readonly ILogService _logService;
        private readonly IGlobalSettingsRegistry _globalSettingsRegistry;
        
        public DependencyManager(ILogService logService, IGlobalSettingsRegistry globalSettingsRegistry)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _globalSettingsRegistry = globalSettingsRegistry ?? throw new ArgumentNullException(nameof(globalSettingsRegistry));
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
        /// Supports cross-module dependencies and ComboBox value dependencies.
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
            
            // First try to find in provided settings, then in global registry
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId) ?? 
                         _globalSettingsRegistry.GetSetting(settingId);
            
            if (setting == null)
            {
                _logService.Log(LogLevel.Warning, $"Setting with ID '{settingId}' not found in local or global registry");
                return unsatisfiedDependencies;
            }
            
            if (setting.Dependencies == null || !setting.Dependencies.Any())
            {
                return unsatisfiedDependencies; // No dependencies
            }
            
            // Find all settings that this setting depends on
            foreach (var dependency in setting.Dependencies)
            {
                var requiredSetting = FindRequiredSetting(dependency, allSettings);
                if (requiredSetting == null)
                {
                    _logService.Log(LogLevel.Warning, $"Required setting '{dependency.RequiredSettingId}' not found for dependency");
                    continue;
                }
                
                bool isDependencySatisfied = IsDependencySatisfied(dependency, requiredSetting);
                if (!isDependencySatisfied)
                {
                    unsatisfiedDependencies.Add(requiredSetting);
                    _logService.Log(LogLevel.Info, $"Unsatisfied dependency: '{setting.Name}' requires '{requiredSetting.Name}'");
                }
            }
            
            return unsatisfiedDependencies;
        }
        
        /// <summary>
        /// Finds the required setting for a dependency, supporting cross-module lookups.
        /// </summary>
        /// <param name="dependency">The dependency to find the required setting for</param>
        /// <param name="allSettings">Local settings to search first</param>
        /// <returns>The required setting if found, null otherwise</returns>
        private ISettingItem? FindRequiredSetting(SettingDependency dependency, IEnumerable<ISettingItem> allSettings)
        {
            // First try to find in provided settings
            var requiredSetting = allSettings.FirstOrDefault(s => s.Id == dependency.RequiredSettingId);
            if (requiredSetting != null)
            {
                return requiredSetting;
            }
            
            // If not found and RequiredModule is specified, search in that module
            if (!string.IsNullOrEmpty(dependency.RequiredModule))
            {
                requiredSetting = _globalSettingsRegistry.GetSetting(dependency.RequiredSettingId, dependency.RequiredModule);
                if (requiredSetting != null)
                {
                    _logService.Log(LogLevel.Debug, $"Found cross-module dependency: '{dependency.RequiredSettingId}' in module '{dependency.RequiredModule}'");
                    return requiredSetting;
                }
            }
            
            // Last resort: search in all modules
            requiredSetting = _globalSettingsRegistry.GetSetting(dependency.RequiredSettingId);
            return requiredSetting;
        }
        
        /// <summary>
        /// Checks if a dependency is satisfied based on its type and required value.
        /// </summary>
        /// <param name="dependency">The dependency to check</param>
        /// <param name="requiredSetting">The required setting</param>
        /// <returns>True if the dependency is satisfied, false otherwise</returns>
        private bool IsDependencySatisfied(SettingDependency dependency, ISettingItem requiredSetting)
        {
            switch (dependency.DependencyType)
            {
                case SettingDependencyType.RequiresEnabled:
                    return requiredSetting.IsSelected;
                    
                case SettingDependencyType.RequiresDisabled:
                    return !requiredSetting.IsSelected;
                    
                case SettingDependencyType.RequiresSpecificValue:
                    if (string.IsNullOrEmpty(dependency.RequiredValue))
                    {
                        _logService.Log(LogLevel.Warning, $"RequiresSpecificValue dependency for '{dependency.RequiredSettingId}' has no RequiredValue specified");
                        return false;
                    }
                    
                    // For ComboBox settings, check SelectedValue
                    if (requiredSetting.ControlType == ControlType.ComboBox)
                    {
                        var currentValue = requiredSetting.SelectedValue?.ToString();
                        bool isValueMatch = string.Equals(currentValue, dependency.RequiredValue, StringComparison.OrdinalIgnoreCase);
                        
                        return isValueMatch;
                    }
                    
                    _logService.Log(LogLevel.Warning, $"RequiresSpecificValue dependency for '{dependency.RequiredSettingId}' is not a ComboBox setting");
                    return false;
                    
                default:
                    _logService.Log(LogLevel.Warning, $"Unknown dependency type: {dependency.DependencyType}");
                    return false;
            }
        }
        
        /// <summary>
        /// Enables all dependencies for a specific setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting whose dependencies need to be enabled.</param>
        /// <param name="allSettings">All available settings.</param>
        /// <returns>True if all dependencies were enabled successfully; otherwise, false.</returns>
        public bool EnableDependenciesForSetting(string settingId, IEnumerable<ISettingItem> allSettings)
        {
            bool allSucceeded = true;
            
            // First try to find in provided settings, then in global registry
            var setting = allSettings.FirstOrDefault(s => s.Id == settingId) ?? 
                         _globalSettingsRegistry.GetSetting(settingId);
            
            if (setting == null || setting.Dependencies == null || !setting.Dependencies.Any())
            {
                return true; // No dependencies to enable
            }
            
            // Process each dependency
            foreach (var dependency in setting.Dependencies)
            {
                var requiredSetting = FindRequiredSetting(dependency, allSettings);
                if (requiredSetting == null)
                {
                    _logService.Log(LogLevel.Warning, $"Required setting '{dependency.RequiredSettingId}' not found for dependency");
                    allSucceeded = false;
                    continue;
                }
                
                bool isDependencySatisfied = IsDependencySatisfied(dependency, requiredSetting);
                if (!isDependencySatisfied)
                {
                    _logService.Log(LogLevel.Info, $"Automatically enabling dependency: {requiredSetting.Name}");
                    
                    // Enable the dependency based on its type
                    if (dependency.DependencyType == SettingDependencyType.RequiresSpecificValue)
                    {
                        // For ComboBox settings with specific value requirements
                        if (requiredSetting.ControlType == ControlType.ComboBox && !string.IsNullOrEmpty(dependency.RequiredValue))
                        {
                            requiredSetting.SelectedValue = dependency.RequiredValue;
                            _logService.Log(LogLevel.Info, $"Set '{requiredSetting.Name}' to value '{dependency.RequiredValue}'");
                        }
                        else
                        {
                            requiredSetting.IsSelected = true;
                        }
                    }
                    else
                    {
                        // For binary toggle dependencies
                        requiredSetting.IsSelected = dependency.DependencyType == SettingDependencyType.RequiresEnabled;
                    }
                    
                    // Apply the setting
                    requiredSetting.ApplySettingCommand?.Execute(null);
                    
                    // Verify the dependency is now satisfied
                    if (!IsDependencySatisfied(dependency, requiredSetting))
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to satisfy dependency for '{requiredSetting.Name}'");
                        allSucceeded = false;
                    }
                }
            }
            
            return allSucceeded;
        }
        
        /// <summary>
        /// Enables all dependencies in the provided list (legacy method for backward compatibility).
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
                dependency.IsSelected = true;
                
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
                                      (d.DependencyType == SettingDependencyType.RequiresEnabled ||
                                       d.DependencyType == SettingDependencyType.RequiresSpecificValue)));
            
            foreach (var dependentSetting in dependentSettings)
            {
                if (dependentSetting.IsSelected)
                {
                    _logService.Log(LogLevel.Info, $"Automatically disabling '{dependentSetting.Name}' as '{settingId}' was disabled or changed");
                    
                    // Only update the UI state, don't apply the setting during dependency management
                    // The setting will be applied when the user explicitly interacts with it
                    var updateMethod = dependentSetting.GetType().GetMethod("UpdateUIStateFromRegistry");
                    if (updateMethod != null)
                    {
                        // Use reflection to call UpdateUIStateFromRegistry to avoid triggering ApplySetting
                        updateMethod.Invoke(dependentSetting, new object[] { false, null, RegistrySettingStatus.NotApplied, null });
                    }
                    else
                    {
                        // Fallback for setting types that don't have UpdateUIStateFromRegistry
                        dependentSetting.IsSelected = false;
                    }
                    
                    // Recursively handle any settings that depend on this one
                    HandleSettingDisabled(dependentSetting.Id, allSettings);
                }
            }
        }
        
        /// <summary>
        /// Handles the value change of a setting by automatically disabling dependent settings
        /// that require specific values that are no longer satisfied.
        /// </summary>
        /// <param name="settingId">The ID of the setting whose value was changed.</param>
        /// <param name="allSettings">All available settings that might depend on the changed setting.</param>
        public void HandleSettingValueChanged(string settingId, IEnumerable<ISettingItem> allSettings)
        {
            if (string.IsNullOrEmpty(settingId))
            {
                _logService.Log(LogLevel.Warning, "Cannot handle value change for null or empty setting ID");
                return;
            }
            
            // Find the setting that was changed
            var changedSetting = allSettings.FirstOrDefault(s => s.Id == settingId);
            if (changedSetting == null)
            {
                _logService.Log(LogLevel.Warning, $"Changed setting '{settingId}' not found");
                return;
            }
            
            // Find all settings that depend on this setting with RequiresSpecificValue
            var dependentSettings = allSettings.Where(s => 
                s.Dependencies != null && 
                s.Dependencies.Any(d => d.RequiredSettingId == settingId && 
                                      d.DependencyType == SettingDependencyType.RequiresSpecificValue));
            
            foreach (var dependentSetting in dependentSettings)
            {
                if (dependentSetting.IsSelected)
                {
                    // Check if the dependency is still satisfied
                    var dependency = dependentSetting.Dependencies.First(d => 
                        d.RequiredSettingId == settingId && 
                        d.DependencyType == SettingDependencyType.RequiresSpecificValue);
                    
                    bool isDependencySatisfied = IsDependencySatisfied(dependency, changedSetting);
                    
                    if (!isDependencySatisfied)
                    {
                        _logService.Log(LogLevel.Info, $"Automatically disabling '{dependentSetting.Name}' as '{settingId}' no longer has required value '{dependency.RequiredValue}'");
                        
                        // Only update the UI state, don't apply the setting during dependency management
                        // The setting will be applied when the user explicitly interacts with it
                        var updateMethod = dependentSetting.GetType().GetMethod("UpdateUIStateFromRegistry");
                        if (updateMethod != null)
                        {
                            // Use reflection to call UpdateUIStateFromRegistry to avoid triggering ApplySetting
                            updateMethod.Invoke(dependentSetting, new object[] { false, null, RegistrySettingStatus.NotApplied, null });
                        }
                        else
                        {
                            // Fallback for setting types that don't have UpdateUIStateFromRegistry
                            dependentSetting.IsSelected = false;
                        }
                        
                        // Recursively handle any settings that depend on this one
                        HandleSettingDisabled(dependentSetting.Id, allSettings);
                    }
                }
            }
        }
    }
}
