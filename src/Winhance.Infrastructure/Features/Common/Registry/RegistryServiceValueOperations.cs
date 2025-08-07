using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    /// <summary>
    /// Registry service implementation for value operations.
    /// </summary>
    public partial class RegistryService
    {
        /// <summary>
        /// Sets a value in the registry.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to set.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="valueKind">The type of the value.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public bool SetValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                if (!CheckWindowsPlatform())
                    return false;

                _logService.Log(LogLevel.Info, $"Setting registry value: {keyPath}\\{valueName}");

                // First ensure the key exists with full access rights
                string[] pathParts = keyPath.Split('\\');
                RegistryKey? rootKey = GetRootKey(pathParts[0]);

                if (rootKey == null)
                {
                    _logService.Log(LogLevel.Error, $"Invalid root key: {pathParts[0]}");
                    return false;
                }

                string subKeyPath = string.Join('\\', pathParts.Skip(1));

                // Create the key with direct Registry API and security settings
                // This will also attempt to take ownership if needed
                RegistryKey? targetKey = EnsureKeyWithFullAccess(rootKey, subKeyPath);

                if (targetKey == null)
                {
                    _logService.Log(LogLevel.Warning, $"Could not open or create registry key: {keyPath}");
                    
                    // Try using PowerShell as a fallback for policy keys
                    if (keyPath.Contains("Policies", StringComparison.OrdinalIgnoreCase))
                    {
                        _logService.Log(LogLevel.Info, $"Attempting to set policy registry value using PowerShell: {keyPath}\\{valueName}");
                        return SetValueUsingPowerShell(keyPath, valueName, value, valueKind);
                    }
                    
                    return false;
                }

                using (targetKey)
                {
                    try
                    {
                        targetKey.SetValue(valueName, value, valueKind);

                        // No caching - direct registry access only
                        _logService.Log(LogLevel.Debug, $"Registry value set: {keyPath}\\{valueName}");
                        _logService.Log(LogLevel.Debug, $"Registry cache cleared for: {keyPath}\\{valueName}");

                        _logService.Log(LogLevel.Success, $"Successfully set registry value: {keyPath}\\{valueName}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Error, $"Failed to set registry value even after taking ownership: {ex.Message}");

                        // Try using PowerShell as a fallback
                        return SetValueUsingPowerShell(keyPath, valueName, value, valueKind);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting registry value {keyPath}\\{valueName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a value directly from the registry without caching.
        /// Always returns the current value from the Windows Registry.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to get.</param>
        /// <returns>The current value from the registry, or null if it doesn't exist.</returns>
        public object? GetValue(string keyPath, string valueName)
        {
            try
            {
                if (!CheckWindowsPlatform())
                    return null;

                _logService.Log(LogLevel.Debug, $"Getting registry value: {keyPath}\\{valueName}");

                // Use direct Windows Registry API to ensure we get the most up-to-date value
                string[] pathParts = keyPath.Split('\\');
                RegistryKey? rootKey = GetRootKey(pathParts[0]);

                if (rootKey == null)
                {
                    _logService.Log(LogLevel.Error, $"Invalid root key: {pathParts[0]}");
                    return null;
                }

                string subKeyPath = string.Join('\\', pathParts.Skip(1));

                // Open the key directly with the Windows Registry API
                using (RegistryKey? key = rootKey.OpenSubKey(subKeyPath, false))
                {
                    if (key == null)
                    {
                        _logService.Log(LogLevel.Debug, $"Registry key not found: {keyPath}");
                        return null;
                    }

                    // Get the value directly from the registry
                    object? value = key.GetValue(valueName);
                    _logService.Log(LogLevel.Debug, $"Registry value for {keyPath}\\{valueName}: {value}");
                    return value;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting registry value {keyPath}\\{valueName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a value from the registry.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to delete.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public bool DeleteValue(string keyPath, string valueName)
        {
            try
            {
                if (!CheckWindowsPlatform())
                    return false;

                _logService.Log(LogLevel.Info, $"Deleting registry value: {keyPath}\\{valueName}");

                // Open the key for writing
                using (RegistryKey? key = OpenRegistryKey(keyPath, true))
                {
                    if (key == null)
                    {
                        _logService.Log(LogLevel.Warning, $"Registry key does not exist: {keyPath}");
                        return false;
                    }

                    // Delete the value
                    key.DeleteValue(valueName, false);
                    
                    // No caching - direct registry access only
                    _logService.Log(LogLevel.Debug, $"Registry value deleted: {keyPath}\\{valueName}");
                    _logService.Log(LogLevel.Debug, $"Registry cache cleared for deleted value: {keyPath}\\{valueName}");

                    _logService.Log(LogLevel.Success, $"Successfully deleted registry value: {keyPath}\\{valueName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error deleting registry value {keyPath}\\{valueName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a value from the registry using hive and subkey.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="subKey">The registry subkey.</param>
        /// <param name="valueName">The name of the value to delete.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public async Task<bool> DeleteValue(RegistryHive hive, string subKey, string valueName)
        {
            string keyPath = $"{RegistryExtensions.GetRegistryHiveString(hive)}\\{subKey}";
            return DeleteValue(keyPath, valueName);
        }

        /// <summary>
        /// Checks if a registry value exists by directly accessing the registry.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to check.</param>
        /// <returns>True if the value exists; otherwise, false.</returns>
        public bool ValueExists(string keyPath, string valueName)
        {
            try
            {
                if (!CheckWindowsPlatform())
                    return false;

                _logService.Log(LogLevel.Debug, $"Checking if registry value exists: {keyPath}\\{valueName}");

                // Open the key for reading directly
                using (RegistryKey? key = OpenRegistryKey(keyPath, false))
                {
                    if (key == null)
                    {
                        _logService.Log(LogLevel.Debug, $"Registry key not found: {keyPath}");
                        return false;
                    }

                    // Check if the value exists directly from the registry
                    string[] valueNames = key.GetValueNames();
                    bool exists = valueNames.Contains(valueName, StringComparer.OrdinalIgnoreCase);
                    
                    _logService.Log(LogLevel.Debug, $"Registry value {keyPath}\\{valueName} exists: {exists}");
                    return exists;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking if registry value exists {keyPath}\\{valueName}: {ex.Message}");
                return false;
            }
        }
    }
}
