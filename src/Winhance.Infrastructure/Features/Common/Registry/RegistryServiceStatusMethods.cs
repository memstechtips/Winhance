using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    public partial class RegistryService
    {
        // Cache for registry key existence to avoid repeated checks
        private readonly Dictionary<string, bool> _keyExistsCache = new Dictionary<string, bool>();

        // Cache for registry value existence to avoid repeated checks
        private readonly Dictionary<string, bool> _valueExistsCache = new Dictionary<string, bool>();

        // Cache for registry values to avoid repeated reads
        private readonly Dictionary<string, object?> _valueCache = new Dictionary<string, object?>();

        /// <summary>
        /// Clears all registry caches to ensure fresh reads
        /// </summary>
        public void ClearRegistryCaches()
        {
            lock (_keyExistsCache)
            {
                _keyExistsCache.Clear();
            }

            lock (_valueExistsCache)
            {
                _valueExistsCache.Clear();
            }

            lock (_valueCache)
            {
                _valueCache.Clear();
            }
        }

        public async Task<RegistrySettingStatus> GetSettingStatusAsync(RegistrySetting setting)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return RegistrySettingStatus.Error;
                }

                if (setting == null)
                {
                    _logService.LogWarning("Cannot get status for null registry setting");
                    return RegistrySettingStatus.Unknown;
                }

                string hiveString = GetRegistryHiveString(setting.Hive);
                string fullPath = $"{hiveString}\\{setting.SubKey}";
                string fullValuePath = $"{fullPath}\\{setting.Name}";

                _logService.LogInformation($"Checking registry setting status: {fullValuePath}");

                // Check if the key exists (using cache)
                bool keyExists;
                lock (_keyExistsCache)
                {
                    if (!_keyExistsCache.TryGetValue(fullPath, out keyExists))
                    {
                        keyExists = KeyExists(fullPath);
                        _keyExistsCache[fullPath] = keyExists;
                    }
                }

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
                        _logService.LogInformation($"Key does not exist and AbsenceMeansEnabled is true - marking as Applied");
                        return RegistrySettingStatus.Applied;
                    }

                    // Default behavior: absence means not applied
                    return RegistrySettingStatus.NotApplied;
                }

                // Check if the value exists (using cache)
                bool valueExists;
                lock (_valueExistsCache)
                {
                    if (!_valueExistsCache.TryGetValue(fullValuePath, out valueExists))
                    {
                        valueExists = ValueExists(fullPath, setting.Name);
                        _valueExistsCache[fullValuePath] = valueExists;
                    }
                }

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
                        _logService.LogInformation($"Value does not exist and AbsenceMeansEnabled is true - marking as Applied");
                        return RegistrySettingStatus.Applied;
                    }

                    // Default behavior: absence means not applied
                    return RegistrySettingStatus.NotApplied;
                }

                // If we're here and the action type is Remove, the key/value still exists, so it's not applied
                if (setting.ActionType == RegistryActionType.Remove)
                {
                    return RegistrySettingStatus.NotApplied;
                }

                // Get the current value (using cache)
                object? currentValue;
                lock (_valueCache)
                {
                    if (!_valueCache.TryGetValue(fullValuePath, out currentValue))
                    {
                        currentValue = GetValue(fullPath, setting.Name);
                        _valueCache[fullValuePath] = currentValue;
                    }
                }

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
                _logService.LogError($"Error checking registry setting status: {ex.Message}", ex);
                return RegistrySettingStatus.Error;
            }
        }

        public async Task<Dictionary<string, RegistrySettingStatus>> GetSettingsStatusAsync(IEnumerable<RegistrySetting> settings)
        {
            var results = new Dictionary<string, RegistrySettingStatus>();

            if (settings == null)
            {
                _logService.LogWarning("Cannot get status for null registry settings collection");
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

        public async Task<int> CountAppliedSettingsAsync(IEnumerable<RegistrySetting> settings)
        {
            if (settings == null)
            {
                _logService.LogWarning("Cannot count applied settings for null registry settings collection");
                return 0;
            }

            int count = 0;
            foreach (var setting in settings)
            {
                if (setting == null)
                {
                    continue;
                }

                var status = await GetSettingStatusAsync(setting);
                if (status == RegistrySettingStatus.Applied)
                {
                    count++;
                }
            }

            return count;
        }

        public async Task<object?> GetCurrentValueAsync(RegistrySetting setting)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return null;
                }

                if (setting == null)
                {
                    _logService.LogWarning("Cannot get current value for null registry setting");
                    return null;
                }

                string hiveString = GetRegistryHiveString(setting.Hive);
                string fullPath = $"{hiveString}\\{setting.SubKey}";
                string fullValuePath = $"{fullPath}\\{setting.Name}";

                _logService.LogInformation($"Getting current value for registry setting: {fullValuePath}");

                // Check if the key exists (using cache)
                bool keyExists;
                lock (_keyExistsCache)
                {
                    if (!_keyExistsCache.TryGetValue(fullPath, out keyExists))
                    {
                        keyExists = KeyExists(fullPath);
                        _keyExistsCache[fullPath] = keyExists;
                    }
                }

                if (!keyExists)
                {
                    return null;
                }

                // Check if the value exists (using cache)
                bool valueExists;
                lock (_valueExistsCache)
                {
                    if (!_valueExistsCache.TryGetValue(fullValuePath, out valueExists))
                    {
                        valueExists = ValueExists(fullPath, setting.Name);
                        _valueExistsCache[fullValuePath] = valueExists;
                    }
                }

                if (!valueExists)
                {
                    return null;
                }

                // Get the current value (using cache)
                object? currentValue;
                lock (_valueCache)
                {
                    if (!_valueCache.TryGetValue(fullValuePath, out currentValue))
                    {
                        currentValue = GetValue(fullPath, setting.Name);
                        _valueCache[fullValuePath] = currentValue;
                    }
                }

                return currentValue;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error getting current registry value: {ex.Message}", ex);
                return null;
            }
        }

        private bool CompareValues(object? value1, object? value2)
        {
            if (value1 == null && value2 == null)
            {
                return true;
            }

            if (value1 == null || value2 == null)
            {
                return false;
            }

            // Try to convert numeric types for comparison
            if (IsNumericType(value1) && IsNumericType(value2))
            {
                try
                {
                    // Convert both to long for comparison
                    long longValue1 = Convert.ToInt64(value1);
                    long longValue2 = Convert.ToInt64(value2);
                    return longValue1 == longValue2;
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Error converting numeric values for comparison: {ex.Message}");
                    // Fall through to other comparison methods
                }
            }

            // Handle different types of registry values
            if (value1 is int intValue1 && value2 is int intValue2)
            {
                return intValue1 == intValue2;
            }
            else if (value1 is string strValue1 && value2 is string strValue2)
            {
                return string.Equals(strValue1, strValue2, StringComparison.OrdinalIgnoreCase);
            }
            else if (value1 is byte[] byteArray1 && value2 is byte[] byteArray2)
            {
                return byteArray1.SequenceEqual(byteArray2);
            }
            else if (value1 is bool boolValue1 && value2 is bool boolValue2)
            {
                return boolValue1 == boolValue2;
            }
            else if (value1 is long longValue1 && value2 is long longValue2)
            {
                return longValue1 == longValue2;
            }
            else if (value1 is uint uintValue1 && value2 is uint uintValue2)
            {
                return uintValue1 == uintValue2;
            }
            else if (value1 is ulong ulongValue1 && value2 is ulong ulongValue2)
            {
                return ulongValue1 == ulongValue2;
            }
            else
            {
                // Try string comparison as a last resort
                try
                {
                    return value1.ToString()?.Equals(value2.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
                }
                catch
                {
                    // For other types, use Equals
                    return value1.Equals(value2);
                }
            }
        }

        private bool IsNumericType(object? value)
        {
            if (value == null) return false;

            return value is byte || value is sbyte ||
                   value is short || value is ushort ||
                   value is int || value is uint ||
                   value is long || value is ulong ||
                   value is float || value is double ||
                   value is decimal;
        }

        /// <summary>
        /// Gets the status of linked registry settings.
        /// </summary>
        /// <param name="linkedSettings">The linked registry settings to check.</param>
        /// <returns>The combined status of the linked registry settings.</returns>
        public async Task<RegistrySettingStatus> GetLinkedSettingsStatusAsync(LinkedRegistrySettings linkedSettings)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return RegistrySettingStatus.Error;
                }

                if (linkedSettings == null || linkedSettings.Settings.Count == 0)
                {
                    _logService.LogWarning("Cannot get status for null or empty linked registry settings");
                    return RegistrySettingStatus.Unknown;
                }

                _logService.LogInformation($"Checking status for {linkedSettings.Settings.Count} linked registry settings with logic: {linkedSettings.Logic}");

                // Check status of all settings
                var statuses = new List<RegistrySettingStatus>();
                var settingDetails = new List<(RegistrySetting Setting, RegistrySettingStatus Status, object? CurrentValue)>();

                foreach (var setting in linkedSettings.Settings)
                {
                    var status = await GetSettingStatusAsync(setting);
                    var currentValue = await GetCurrentValueAsync(setting);
                    statuses.Add(status);
                    settingDetails.Add((setting, status, currentValue));
                    _logService.LogInformation($"Registry setting {setting.Hive}\\{setting.SubKey}\\{setting.Name} status: {status}, current value: {currentValue ?? "null"}");
                }

                // If any setting is in error state, return error
                if (statuses.Any(s => s == RegistrySettingStatus.Error))
                {
                    return RegistrySettingStatus.Error;
                }

                // If any setting is unknown, return unknown
                if (statuses.Any(s => s == RegistrySettingStatus.Unknown))
                {
                    return RegistrySettingStatus.Unknown;
                }

                // Apply the appropriate logic based on the LinkedSettingsLogic property
                switch (linkedSettings.Logic)
                {
                    case LinkedSettingsLogic.Primary:
                        // Find the primary setting (if any)
                        var primarySetting = settingDetails.FirstOrDefault(s => s.Setting.IsPrimary);
                        if (primarySetting != default)
                        {
                            _logService.LogInformation($"Using primary setting {primarySetting.Setting.Name} with status {primarySetting.Status}");
                            return primarySetting.Status;
                        }
                        else
                        {
                            // If no primary setting is marked, use the first one
                            _logService.LogInformation($"No primary setting found, using first setting with status {settingDetails.First().Status}");
                            return settingDetails.First().Status;
                        }

                    case LinkedSettingsLogic.All:
                        // All settings must be applied for the entire setting to be considered applied
                        if (statuses.All(s => s == RegistrySettingStatus.Applied))
                        {
                            _logService.LogInformation("All settings are applied - marking as Applied");
                            return RegistrySettingStatus.Applied;
                        }
                        else if (statuses.All(s => s == RegistrySettingStatus.NotApplied))
                        {
                            _logService.LogInformation("All settings are not applied - marking as NotApplied");
                            return RegistrySettingStatus.NotApplied;
                        }
                        else
                        {
                            _logService.LogInformation("Mixed status with 'All' logic - marking as Modified");
                            return RegistrySettingStatus.Modified;
                        }

                    case LinkedSettingsLogic.Any:
                        // If any setting is applied, the entire setting is considered applied
                        if (statuses.Any(s => s == RegistrySettingStatus.Applied))
                        {
                            _logService.LogInformation("At least one setting is applied - marking as Applied");
                            return RegistrySettingStatus.Applied;
                        }
                        else if (statuses.All(s => s == RegistrySettingStatus.NotApplied))
                        {
                            _logService.LogInformation("All settings are not applied - marking as NotApplied");
                            return RegistrySettingStatus.NotApplied;
                        }
                        else
                        {
                            _logService.LogInformation("Mixed status with 'Any' logic - marking as Modified");
                            return RegistrySettingStatus.Modified;
                        }

                    case LinkedSettingsLogic.Custom:
                        // Special handling for settings with multiple registry entries
                        if (linkedSettings.Settings.Count > 1)
                        {
                            // Group settings by their purpose (HKCU vs HKLM, etc.)
                            var hkcuSettings = settingDetails.Where(s => s.Setting.Hive == RegistryHive.CurrentUser).ToList();
                            var hklmSettings = settingDetails.Where(s => s.Setting.Hive == RegistryHive.LocalMachine).ToList();

                            // For settings like "Personalized Ads" that have both HKCU and HKLM components
                            if (hkcuSettings.Count > 0 && hklmSettings.Count > 0)
                            {
                                _logService.LogInformation("Found mixed HKCU/HKLM settings - using special handling logic");

                                // Check if any of the settings are in the Applied state
                                bool anyApplied = statuses.Any(s => s == RegistrySettingStatus.Applied);
                                bool allNotApplied = statuses.All(s => s == RegistrySettingStatus.NotApplied);

                                // For settings like "Personalized Ads", if any part is Applied, consider the whole setting Applied
                                // This matches Windows behavior where some registry keys might not exist until changed
                                if (anyApplied)
                                {
                                    _logService.LogInformation("At least one component is applied - marking as Applied");
                                    return RegistrySettingStatus.Applied;
                                }
                                else if (allNotApplied)
                                {
                                    _logService.LogInformation("All components are not applied - marking as NotApplied");
                                    return RegistrySettingStatus.NotApplied;
                                }
                                else
                                {
                                    _logService.LogInformation("Mixed status - marking as Modified");
                                    return RegistrySettingStatus.Modified;
                                }
                            }
                        }

                        // Fall back to standard logic for Custom if no special case was handled
                        goto default;

                    default:
                        // Standard logic for single registry settings or settings with similar components
                        // If all settings are applied, return applied
                        if (statuses.All(s => s == RegistrySettingStatus.Applied))
                        {
                            return RegistrySettingStatus.Applied;
                        }

                        // If all settings are not applied, return not applied
                        if (statuses.All(s => s == RegistrySettingStatus.NotApplied))
                        {
                            return RegistrySettingStatus.NotApplied;
                        }

                        // If some settings are applied and some are not, return modified
                        return RegistrySettingStatus.Modified;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking linked registry settings status: {ex.Message}", ex);
                return RegistrySettingStatus.Error;
            }
        }

        /// <summary>
        /// Gets the status of an optimization setting that may contain multiple registry settings.
        /// </summary>
        /// <param name="setting">The optimization setting to check.</param>
        /// <returns>The combined status of the optimization setting.</returns>
        public async Task<RegistrySettingStatus> GetOptimizationSettingStatusAsync(Winhance.Core.Features.Optimize.Models.OptimizationSetting setting)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return RegistrySettingStatus.Error;
                }

                if (setting == null)
                {
                    _logService.LogWarning("Cannot get status for null optimization setting");
                    return RegistrySettingStatus.Unknown;
                }

                _logService.LogInformation($"Checking status for optimization setting: {setting.Name}");

                // If the setting has registry settings collection
                if (setting.RegistrySettings != null && setting.RegistrySettings.Count > 0)
                {
                    // Create a LinkedRegistrySettings object from the RegistrySettings collection
                    var linkedSettings = setting.CreateLinkedRegistrySettings();
                    return await GetLinkedSettingsStatusAsync(linkedSettings);
                }

                _logService.LogWarning($"Optimization setting {setting.Name} has no registry settings");
                return RegistrySettingStatus.Unknown;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking optimization setting status: {ex.Message}", ex);
                return RegistrySettingStatus.Error;
            }
        }

        /// <summary>
        /// Applies linked registry settings.
        /// </summary>
        /// <param name="linkedSettings">The linked registry settings to apply.</param>
        /// <param name="enable">Whether to enable or disable the settings.</param>
        /// <returns>True if all settings were applied successfully; otherwise, false.</returns>
        public async Task<bool> ApplyLinkedSettingsAsync(LinkedRegistrySettings linkedSettings, bool enable)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                if (linkedSettings == null || linkedSettings.Settings.Count == 0)
                {
                    _logService.LogWarning("Cannot apply null or empty linked registry settings");
                    return false;
                }

                _logService.LogInformation($"Applying {linkedSettings.Settings.Count} linked registry settings, enable={enable}");

                // Clear caches before applying settings to ensure fresh reads after changes
                ClearRegistryCaches();

                bool allSucceeded = true;
                foreach (var setting in linkedSettings.Settings)
                {
                    string hiveString = GetRegistryHiveString(setting.Hive);
                    string fullPath = $"{hiveString}\\{setting.SubKey}";

                    // Ensure the registry key exists before trying to set a value
                    if (!KeyExists(fullPath))
                    {
                        _logService.LogInformation($"Registry key does not exist, creating: {fullPath}");
                        bool keyCreated = CreateKey(fullPath);
                        if (!keyCreated)
                        {
                            _logService.LogWarning($"Failed to create registry key: {fullPath}");
                            allSucceeded = false;
                            continue;
                        }
                    }

                    // Check if this is a Group Policy registry key that needs special handling
                    if (setting.IsGroupPolicy)
                    {
                        if (enable)
                        {
                            // When enabling a Group Policy setting, we need to delete the policy key
                            // This removes the policy restriction and allows Windows to use its default behavior
                            _logService.LogInformation($"Enabling by deleting Group Policy registry key: {fullPath}");
                            bool success = DeleteKey(fullPath);
                            if (!success)
                            {
                                _logService.LogWarning($"Failed to delete Group Policy registry key: {fullPath}");
                                allSucceeded = false;
                            }
                            continue;
                        }
                        else
                        {
                            // When disabling, we'll create the key and set the value below
                            // This applies the policy restriction
                            _logService.LogInformation($"Disabling by creating/setting Group Policy registry key: {fullPath}");
                            // Continue with normal processing to set the value
                        }
                    }

                    // Special handling for ActionType.Remove settings
                    if (setting.ActionType == RegistryActionType.Remove)
                    {
                        if (enable)
                        {
                            // When enabling a setting with ActionType.Remove, we need to create the key
                            // For registry keys that control visibility by their existence
                            _logService.LogInformation($"Creating registry key for ActionType.Remove setting: {fullPath}");
                            
                            // If the Name is a GUID, it's likely a subkey that needs to be created
                            if (Guid.TryParse(setting.Name.Trim('{', '}'), out _))
                            {
                                // Create a subkey with the GUID name
                                string fullKeyPath = $"{fullPath}\\{setting.Name}";
                                _logService.LogInformation($"Creating GUID subkey: {fullKeyPath}");
                                bool success = CreateKey(fullKeyPath);
                                if (!success)
                                {
                                    _logService.LogWarning($"Failed to create registry key: {fullKeyPath}");
                                    allSucceeded = false;
                                }
                                else
                                {
                                    _logService.LogSuccess($"Successfully created registry key: {fullKeyPath}");
                                }
                            }
                            else
                            {
                                // Just create the main key
                                bool success = CreateKey(fullPath);
                                if (!success)
                                {
                                    _logService.LogWarning($"Failed to create registry key: {fullPath}");
                                    allSucceeded = false;
                                }
                                else
                                {
                                    _logService.LogSuccess($"Successfully created registry key: {fullPath}");
                                }
                            }
                            continue;
                        }
                        else
                        {
                            // When disabling, delete the key or value
                            if (Guid.TryParse(setting.Name.Trim('{', '}'), out _))
                            {
                                // Delete the GUID subkey
                                string fullKeyPath = $"{fullPath}\\{setting.Name}";
                                _logService.LogInformation($"Deleting GUID subkey: {fullKeyPath}");
                                bool success = DeleteKey(fullKeyPath);
                                if (!success)
                                {
                                    _logService.LogWarning($"Failed to delete registry key: {fullKeyPath}");
                                    allSucceeded = false;
                                }
                                else
                                {
                                    _logService.LogSuccess($"Successfully deleted registry key: {fullKeyPath}");
                                }
                            }
                            else
                            {
                                // Delete the value
                                _logService.LogInformation($"Deleting registry value: {fullPath}\\{setting.Name}");
                                bool success = DeleteValue(fullPath, setting.Name);
                                if (!success)
                                {
                                    _logService.LogWarning($"Failed to delete registry value: {fullPath}\\{setting.Name}");
                                    allSucceeded = false;
                                }
                                else
                                {
                                    _logService.LogSuccess($"Successfully deleted registry value: {fullPath}\\{setting.Name}");
                                }
                            }
                            continue;
                        }
                    }

                    // Standard handling for regular registry settings
                    object valueToSet;
                    if (enable)
                    {
                        // When enabling, use EnabledValue if available, otherwise fall back to RecommendedValue
                        valueToSet = setting.EnabledValue ?? setting.RecommendedValue;
                        _logService.LogInformation($"Setting {fullPath}\\{setting.Name} to enabled value: {valueToSet}");
                    }
                    else
                    {
                        // When disabling, use DisabledValue if available, otherwise fall back to DefaultValue
                        object? disabledValue = setting.DisabledValue ?? setting.DefaultValue;

                        // If disabledValue is null, delete the value
                        if (disabledValue == null)
                        {
                            _logService.LogInformation($"Deleting registry value: {fullPath}\\{setting.Name}");
                            bool success = DeleteValue(fullPath, setting.Name);
                            if (!success)
                            {
                                _logService.LogWarning($"Failed to delete registry value: {fullPath}\\{setting.Name}");
                                allSucceeded = false;
                            }
                            continue;
                        }

                        valueToSet = disabledValue;
                        _logService.LogInformation($"Setting {fullPath}\\{setting.Name} to disabled value: {valueToSet}");
                    }

                    // Apply the registry change
                    bool result = SetValue(fullPath, setting.Name, valueToSet, setting.ValueType);
                    if (!result)
                    {
                        _logService.LogWarning($"Failed to set registry value: {fullPath}\\{setting.Name}");
                        allSucceeded = false;
                    }
                }

                // Clear caches again after applying settings to ensure fresh reads
                ClearRegistryCaches();

                return allSucceeded;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error applying linked registry settings: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Applies an optimization setting that may contain multiple registry settings.
        /// </summary>
        /// <param name="setting">The optimization setting to apply.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        /// <returns>True if the setting was applied successfully; otherwise, false.</returns>
        public async Task<bool> ApplyOptimizationSettingAsync(Winhance.Core.Features.Optimize.Models.OptimizationSetting setting, bool enable)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                if (setting == null)
                {
                    _logService.LogWarning("Cannot apply null optimization setting");
                    return false;
                }

                _logService.LogInformation($"Applying optimization setting: {setting.Name}, enable={enable}");

                // If the setting has registry settings collection
                if (setting.RegistrySettings != null && setting.RegistrySettings.Count > 0)
                {
                    // Create a LinkedRegistrySettings object from the RegistrySettings collection
                    var linkedSettings = setting.CreateLinkedRegistrySettings();
                    return await ApplyLinkedSettingsAsync(linkedSettings, enable);
                }

                _logService.LogWarning($"Optimization setting {setting.Name} has no registry settings to apply");
                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error applying optimization setting: {ex.Message}", ex);
                return false;
            }
        }
    }
}
