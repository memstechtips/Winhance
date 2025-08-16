using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    public partial class RegistryService
    {
        public async Task<RegistrySettingStatus> GetSettingStatusAsync(RegistrySetting setting)
        {
            try
            {
                if (!CheckWindowsPlatform())
                {
                    _logService.Log(LogLevel.Error, "Registry operations are only supported on Windows");
                    return RegistrySettingStatus.Error;
                }

                if (setting == null)
                {
                    _logService.Log(LogLevel.Warning, "Cannot get status for null registry setting");
                    return RegistrySettingStatus.Unknown;
                }

                string fullPath = $"{setting.Hive}\\{setting.SubKey}";
                string fullValuePath = $"{fullPath}\\{setting.Name}";

                // Removed excessive logging for registry setting status checks

                // Check if the key exists (direct registry access)
                bool keyExists = KeyExists(fullPath);
                // Removed excessive debug logging for registry key existence check

                // Handle non-existence of the key
                if (!keyExists)
                {
                    // For Remove actions, non-existence of the key is considered "Applied"
                    if (setting.ActionType == RegistryActionType.Remove)
                    {
                        return RegistrySettingStatus.Applied;
                    }

                    // For settings where absence means enabled (like HttpAcceptLanguageOptOut)
                    if (setting.AbsenceMeansEnabled)
                    {
                        _logService.Log(LogLevel.Info, $"Key does not exist and AbsenceMeansEnabled is true - marking as Applied");
                        return RegistrySettingStatus.Applied;
                    }

                    // Default behavior: absence means not applied
                    return RegistrySettingStatus.NotApplied;
                }

                // Check if the value exists (using cache)
                // Check if the value exists (direct registry access)
                bool valueExists = ValueExists(fullPath, setting.Name);
                // Removed excessive debug logging for registry value existence check

                // Handle non-existence of the value
                if (!valueExists)
                {
                    // For Remove actions, non-existence of the value is considered "Applied"
                    if (setting.ActionType == RegistryActionType.Remove)
                    {
                        return RegistrySettingStatus.Applied;
                    }

                    // For settings where absence means enabled (like HttpAcceptLanguageOptOut)
                    if (setting.AbsenceMeansEnabled)
                    {
                        _logService.Log(LogLevel.Info, $"Value does not exist and AbsenceMeansEnabled is true - marking as Applied");
                        return RegistrySettingStatus.Applied;
                    }

                    // Default behavior: absence means not applied
                    return RegistrySettingStatus.NotApplied;
                }

                // If we're here and the action type is Remove, the key/value still exists
                if (setting.ActionType == RegistryActionType.Remove)
                {
                    // Special handling for GUID subkeys in NameSpace registry paths
                    if (setting.IsGuidSubkey && 
                        (setting.SubKey.EndsWith("NameSpace") || setting.SubKey.Contains("NameSpace\\")))
                    {
                        // For these special GUID subkeys, we need to check if the subkey exists
                        string guidSubkeyPath = $"{fullPath}\\{setting.Name}";
                        bool guidSubkeyExists = KeyExists(guidSubkeyPath);
                        
                        _logService.Log(LogLevel.Info, $"Checking GUID subkey existence: {guidSubkeyPath}, Exists={guidSubkeyExists}");
                        
                        // If the GUID subkey exists, the feature is enabled (toggle should be ON)
                        if (guidSubkeyExists)
                        {
                            _logService.Log(LogLevel.Info, $"GUID subkey exists - marking as Applied");
                            return RegistrySettingStatus.Applied;
                        }
                        else
                        {
                            _logService.Log(LogLevel.Info, $"GUID subkey does not exist - marking as NotApplied");
                            return RegistrySettingStatus.NotApplied;
                        }
                    }
                    
                    // For normal Remove actions, key existing means not applied
                    return RegistrySettingStatus.NotApplied;
                }

                // Get the current value (using cache)
                // Get the current value directly from the registry
                object? currentValue = GetValue(fullPath, setting.Name);
                // Removed excessive debug logging for registry value retrieval

                // If the value is null, consider it not applied
                if (currentValue == null)
                {
                    return RegistrySettingStatus.NotApplied;
                }

                // Compare with enabled value (use EnabledValue if available, otherwise fall back to RecommendedValue)
                object valueToCompare = setting.EnabledValue ?? setting.RecommendedValue;
                bool matchesEnabled = CompareValues(currentValue, valueToCompare);
                if (matchesEnabled)
                {
                    return RegistrySettingStatus.Applied;
                }

                // If there's a disabled value specified, check if it matches that
                object? disabledValue = setting.DisabledValue ?? setting.DefaultValue;
                if (disabledValue != null)
                {
                    bool matchesDisabled = CompareValues(currentValue, disabledValue);
                    if (matchesDisabled)
                    {
                        return RegistrySettingStatus.NotApplied;
                    }
                }

                // If it doesn't match recommended or default, it's modified
                return RegistrySettingStatus.Modified;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking registry setting status: {ex.Message}");
                return RegistrySettingStatus.Error;
            }
        }

        public async Task<Dictionary<string, RegistrySettingStatus>> GetSettingsStatusAsync(IEnumerable<RegistrySetting> settings)
        {
            var results = new Dictionary<string, RegistrySettingStatus>();

            if (settings == null)
            {
                _logService.Log(LogLevel.Warning, "Cannot get status for null registry settings collection");
                return results;
            }

            foreach (var setting in settings)
            {
                if (setting == null || string.IsNullOrEmpty(setting.Name))
                {
                    continue;
                }

                var status = await GetSettingStatusAsync(setting);
                results[setting.Name] = status;
            }

            return results;
        }

        public async Task<RegistrySettingStatus> GetLinkedSettingsStatusAsync(LinkedRegistrySettings linkedSettings)
        {
            if (linkedSettings == null || linkedSettings.Settings.Count == 0)
                return RegistrySettingStatus.Unknown;

            try
            {
                List<RegistrySettingStatus> statuses = new List<RegistrySettingStatus>();
                
                // Special case: If all settings have ActionType = Remove, we need to handle differently
                bool allRemoveActions = linkedSettings.Settings.All(s => s.ActionType == RegistryActionType.Remove);
                
                if (allRemoveActions)
                {
                    // For Remove actions, we need to check if all keys/values don't exist (for All logic)
                    // or if any key/value doesn't exist (for Any logic)
                    bool allKeysRemoved = true;
                    bool anyKeyRemoved = false;
                    
                    foreach (var setting in linkedSettings.Settings)
                    {
                        string fullPath = $"{setting.Hive}\\{setting.SubKey}";
                        string fullValuePath = $"{fullPath}\\{setting.Name}";
                        
                        // Check if the key exists
                        bool keyExists = KeyExists(fullPath);
                        
                        // For Remove actions, if the key doesn't exist, it's considered "removed" (Applied)
                        if (!keyExists)
                        {
                            anyKeyRemoved = true;
                        }
                        else
                        {
                            // Key exists, now check if the value exists
                            bool valueExists = ValueExists(fullPath, setting.Name);
                            
                            if (!valueExists)
                            {
                                anyKeyRemoved = true;
                            }
                            else
                            {
                                // Special case for 3D Objects toggle which is opposite of normal Remove action
                                // When the key exists, 3D Objects is shown (should be considered Applied)
                                if (setting.Name == "{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}")
                                {
                                    _logService.Log(LogLevel.Info, $"3D Objects key exists in linked settings - special handling");
                                    // For 3D Objects, we want to return Applied when the key exists
                                    // So we don't set allKeysRemoved to false here
                                    continue;
                                }
                                
                                allKeysRemoved = false;
                            }
                        }
                    }
                    
                    // Determine status based on the logic
                    if (linkedSettings.Logic == LinkedSettingsLogic.All)
                    {
                        return allKeysRemoved ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;
                    }
                    else
                    {
                        return anyKeyRemoved ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;
                    }
                }
                
                // Normal case: Process each setting individually
                foreach (var setting in linkedSettings.Settings)
                {
                    var status = await GetSettingStatusAsync(setting);
                    statuses.Add(status);
                }

                // Check if any settings have an error status
                if (statuses.Contains(RegistrySettingStatus.Error))
                {
                    return RegistrySettingStatus.Error;
                }

                // Check if any settings have an unknown status
                if (statuses.Contains(RegistrySettingStatus.Unknown))
                {
                    return RegistrySettingStatus.Unknown;
                }

                // Count how many settings are applied
                int appliedCount = statuses.Count(s => s == RegistrySettingStatus.Applied);
                int notAppliedCount = statuses.Count(s => s == RegistrySettingStatus.NotApplied);
                int modifiedCount = statuses.Count(s => s == RegistrySettingStatus.Modified);

                // Determine the overall status based on the logic type
                switch (linkedSettings.Logic)
                {
                    case LinkedSettingsLogic.All:
                        // All settings must be applied for the overall status to be Applied
                        if (appliedCount == statuses.Count)
                        {
                            return RegistrySettingStatus.Applied;
                        }
                        else if (notAppliedCount == statuses.Count)
                        {
                            return RegistrySettingStatus.NotApplied;
                        }
                        else
                        {
                            return RegistrySettingStatus.Modified;
                        }

                    case LinkedSettingsLogic.Any:
                        // Any applied setting means the overall status is Applied
                        if (appliedCount > 0)
                        {
                            return RegistrySettingStatus.Applied;
                        }
                        else if (notAppliedCount == statuses.Count)
                        {
                            return RegistrySettingStatus.NotApplied;
                        }
                        else
                        {
                            return RegistrySettingStatus.Modified;
                        }

                    case LinkedSettingsLogic.Primary:
                        // Find the primary setting
                        var primarySetting = linkedSettings.Settings.FirstOrDefault(s => s.IsPrimary);
                        if (primarySetting != null)
                        {
                            // Return the status of the primary setting
                            return await GetSettingStatusAsync(primarySetting);
                        }
                        else
                        {
                            // If no primary setting is found, fall back to All logic
                            if (appliedCount == statuses.Count)
                            {
                                return RegistrySettingStatus.Applied;
                            }
                            else if (notAppliedCount == statuses.Count)
                            {
                                return RegistrySettingStatus.NotApplied;
                            }
                            else
                            {
                                return RegistrySettingStatus.Modified;
                            }
                        }

                    default:
                        // Default to Any logic
                        if (appliedCount > 0)
                        {
                            return RegistrySettingStatus.Applied;
                        }
                        else if (notAppliedCount == statuses.Count)
                        {
                            return RegistrySettingStatus.NotApplied;
                        }
                        else
                        {
                            return RegistrySettingStatus.Modified;
                        }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking linked registry settings status: {ex.Message}");
                return RegistrySettingStatus.Error;
            }
        }

        public async Task<RegistrySettingStatus> GetOptimizationSettingStatusAsync(Winhance.Core.Features.Optimize.Models.OptimizationSetting setting)
        {
            if (setting == null)
            {
                return RegistrySettingStatus.Unknown;
            }

            try
            {
                _logService.Log(LogLevel.Info, $"Checking status for optimization setting: {setting.Name}");

                // If the setting has registry settings collection, create a LinkedRegistrySettings and use that
                if (setting.RegistrySettings != null && setting.RegistrySettings.Count > 0)
                {
                    var linkedSettings = new LinkedRegistrySettings
                    {
                        Settings = setting.RegistrySettings.ToList(),
                        Logic = setting.LinkedSettingsLogic
                    };
                    return await GetLinkedSettingsStatusAsync(linkedSettings);
                }
                else
                {
                    _logService.Log(LogLevel.Warning, $"Optimization setting {setting.Name} has no registry settings");
                    return RegistrySettingStatus.Unknown;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking optimization setting status: {ex.Message}");
                return RegistrySettingStatus.Error;
            }
        }

        /// <summary>
        /// Helper method to compare registry values, handling different types.
        /// </summary>
        private bool CompareValues(object? currentValue, object? desiredValue)
        {
            if (currentValue == null && desiredValue == null)
            {
                return true;
            }

            if (currentValue == null || desiredValue == null)
            {
                return false;
            }

            // Handle different types of registry values
            if (currentValue is int intValue && desiredValue is int desiredIntValue)
            {
                return intValue == desiredIntValue;
            }
            else if (currentValue is string strValue && desiredValue is string desiredStrValue)
            {
                return strValue.Equals(desiredStrValue, StringComparison.OrdinalIgnoreCase);
            }
            else if (currentValue is byte[] byteArrayValue && desiredValue is byte[] desiredByteArrayValue)
            {
                return byteArrayValue.SequenceEqual(desiredByteArrayValue);
            }
            else
            {
                // For other types, use the default Equals method
                return currentValue.Equals(desiredValue);
            }
        }
    }
}
