using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    /// <summary>
    /// Provides access to the Windows registry.
    /// This is the main file for the RegistryService, which is split into multiple partial files:
    /// - RegistryServiceCore.cs - Core functionality and constructor
    /// - RegistryServiceKeyOperations.cs - Key creation, deletion, and navigation
    /// - RegistryServiceValueOperations.cs - Value reading and writing
    /// - RegistryServiceStatusMethods.cs - Status checking methods
    /// - RegistryServiceEnsureKey.cs - Key creation with security settings
    /// - RegistryServiceCompletion.cs - Helper methods
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class RegistryService : IRegistryService
    {

        /// <summary>
        /// Applies a registry setting.
        /// </summary>
        /// <param name="setting">The registry setting to apply.</param>
        /// <param name="isEnabled">Whether to enable or disable the setting.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        /// <summary>
        /// Applies a registry setting that involves creating or deleting a GUID subkey.
        /// </summary>
        /// <param name="setting">The registry setting to apply.</param>
        /// <param name="isEnabled">Whether to enable or disable the setting.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        private async Task<bool> ApplyGuidSubkeySetting(RegistrySetting setting, bool isEnabled)
        {
            string parentKeyPath = $"{setting.Hive}\\{setting.SubKey}";
            string guidSubkeyPath = $"{parentKeyPath}\\{setting.Name}";
            
            _logService.Log(
                LogLevel.Info,
                $"Applying GUID subkey setting: {setting.Name}, IsEnabled={isEnabled}, Path={guidSubkeyPath}"
            );
            
            if (isEnabled)
            {
                _logService.Log(LogLevel.Info, $"Creating GUID subkey: {guidSubkeyPath}");

                // Ensure parent exists and then create subkey via native API
                if (!CreateKeyIfNotExists(parentKeyPath))
                {
                    _logService.Log(LogLevel.Warning, $"Failed to ensure parent key exists: {parentKeyPath}");
                    return false;
                }

                using (var parent = OpenRegistryKey(parentKeyPath, true))
                {
                    if (parent == null)
                    {
                        _logService.Log(LogLevel.Warning, $"Could not open parent key for write: {parentKeyPath}");
                        return false;
                    }
                    using var created = parent.CreateSubKey(setting.Name, true);
                    if (created == null)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to create GUID subkey: {guidSubkeyPath}");
                        return false;
                    }
                }
                _logService.Log(LogLevel.Success, $"Successfully created GUID subkey: {guidSubkeyPath}");
                
                // Publish registry change event for key creation
                OnRegistryValueChanged(setting, null, string.Empty);
                
                return true;
            }
            else
            {
                _logService.Log(LogLevel.Info, $"Deleting GUID subkey: {guidSubkeyPath}");
                return DeleteKey(guidSubkeyPath);
            }
        }

        public async Task<bool> ApplySettingAsync(RegistrySetting setting, bool isEnabled)
        {
            if (setting == null)
                return false;

            try
            {
                // Get the current value before change
                var oldValue = GetValue($"{setting.Hive}\\{setting.SubKey}", setting.Name);
                
                // Track enable/disable state (no persisted field needed after PowerShell removal)
                string keyPath = $"{setting.Hive}\\{setting.SubKey}";

                _logService.Log(
                    LogLevel.Info,
                    $"Applying registry setting: {setting.Name}, IsEnabled={isEnabled}, Path={keyPath}, ActionType={setting.ActionType}"
                );
                
                // Special handling for GUID subkeys
                if (setting.IsGuidSubkey)
                {
                    return await ApplyGuidSubkeySetting(setting, isEnabled);
                }

                // Special handling for Remove action type
                if (setting.ActionType == RegistryActionType.Remove)
                {
                    // For Remove action type, we need to either create or delete the key/value
                    string fullKeyPath = $"{keyPath}\\{setting.Name}";
                    
                    if (isEnabled)
                    {
                        _logService.Log(LogLevel.Info, $"Creating registry key/value for Remove action: {fullKeyPath}");
                        if (!CreateKeyIfNotExists(fullKeyPath))
                        {
                            _logService.Log(LogLevel.Warning, $"Failed to create registry key for Remove action: {fullKeyPath}");
                            return false;
                        }
                        OnRegistryValueChanged(setting, null, string.Empty);
                        return true;
                    }
                    else
                    {
                        _logService.Log(LogLevel.Info, $"Deleting registry key/value for Remove action: {fullKeyPath}");
                        return DeleteKey(fullKeyPath);
                    }
                }
                
                // Standard handling for other action types
                object? valueToSet = null;

                if (isEnabled)
                {
                    // When enabling, use EnabledValue if available, otherwise fall back to RecommendedValue
                    valueToSet = setting.EnabledValue ?? setting.RecommendedValue;
                }
                else
                {
                    // When disabling, check if this is a Group Policy setting
                    if (setting.IsGroupPolicy)
                    {
                        // For Group Policy settings, delete the entire registry value when disabling
                        // This ensures Windows recognizes the policy is no longer applied
                        _logService.Log(
                            LogLevel.Info,
                            $"Deleting Group Policy registry value: {keyPath}\\{setting.Name} (ValueType: {setting.ValueType})"
                        );
                        return DeleteValue($"{setting.Hive}\\{setting.SubKey}", setting.Name);
                    }
                    
                    // Standard handling: use DisabledValue if available, otherwise fall back to DefaultValue
                    valueToSet = setting.DisabledValue ?? setting.DefaultValue;
                }

                if (valueToSet == null)
                {
                    // If the value to set is null, delete the value
                    _logService.Log(
                        LogLevel.Warning,
                        $"Value to set for {setting.Name} is null, deleting the value"
                    );
                    return DeleteValue($"{setting.Hive}\\{setting.SubKey}", setting.Name);
                }
                else
                {
                    // Otherwise, set the value
                    _logService.Log(
                        LogLevel.Info,
                        $"Setting {keyPath}\\{setting.Name} to {valueToSet} ({setting.ValueType})"
                    );
                    
                    // Ensure the key exists before setting the value
                    bool keyCreated = CreateKeyIfNotExists(keyPath);
                    if (!keyCreated)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to create registry key: {keyPath}");
                        return false;
                    }
                    
                    // Verify the key exists before proceeding
                    if (!KeyExists(keyPath))
                    {
                        _logService.Log(
                            LogLevel.Error,
                            $"Registry key still does not exist after creation attempt: {keyPath}"
                        );
                        return false;
                    }
                    
                    // Try to set the value
                    bool result = SetValue(keyPath, setting.Name, valueToSet, setting.ValueType);
                    
                    // Verify the value was set correctly
                    if (result)
                    {
                        object? verifyValue = GetValue(keyPath, setting.Name);
                        if (verifyValue == null)
                        {
                            _logService.Log(LogLevel.Warning, $"Value verification failed - value is null after setting: {keyPath}\\{setting.Name}");
                            return false;
                        }
                        
                        _logService.Log(
                            LogLevel.Success,
                            $"Successfully set and verified registry value: {keyPath}\\{setting.Name}"
                        );
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Failed to set {keyPath}\\{setting.Name} to {valueToSet}"
                        );
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying registry setting {setting.Name}: {ex.Message}"
                );
                return false;
            }
        }

        /// <summary>
        /// Applies linked registry settings.
        /// </summary>
        /// <param name="linkedSettings">The linked registry settings to apply.</param>
        /// <param name="isEnabled">Whether to enable or disable the settings.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public async Task<bool> ApplyLinkedSettingsAsync(
            LinkedRegistrySettings linkedSettings,
            bool isEnabled
        )
        {
            if (linkedSettings == null || linkedSettings.Settings.Count == 0)
                return false;

            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying {linkedSettings.Settings.Count} linked registry settings, IsEnabled={isEnabled}"
                );

                bool allSuccess = true;
                int settingCount = 0;
                int totalSettings = linkedSettings.Settings.Count;

                foreach (var setting in linkedSettings.Settings)
                {
                    settingCount++;

                    // Ensure the registry key path exists before applying the setting
                    string keyPath = $"{setting.Hive}\\{setting.SubKey}";
                    bool keyExists = KeyExists(keyPath);
                    
                    if (!keyExists)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            $"Registry key does not exist: {keyPath}, creating it"
                        );
                        
                        // Try to create the key
                        bool keyCreated = CreateKeyIfNotExists(keyPath);
                        
                        if (!keyCreated)
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                $"Failed to create registry key: {keyPath} using standard method, trying PowerShell"
                            );
                            
                            // If we couldn't create the key, try using PowerShell to create it
                            // This will be handled in the ApplySettingAsync method
                        }
                    }

                    bool success = await ApplySettingAsync(setting, isEnabled);

                    if (!success)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Failed to apply linked setting: {setting.Name}"
                        );

                        // If the logic is All, we need all settings to succeed
                        if (linkedSettings.Logic == LinkedSettingsLogic.All)
                        {
                            allSuccess = false;
                        }
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Success,
                            $"Successfully applied linked setting {settingCount}/{totalSettings}: {setting.Name}"
                        );
                    }
                }

                _logService.Log(
                    allSuccess ? LogLevel.Success : LogLevel.Warning,
                    $"Completed applying linked settings with result: {(allSuccess ? "Success" : "Some settings failed")}"
                );

                return allSuccess;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying linked registry settings: {ex.Message}"
                );
                return false;
            }
        }

        /// <summary>
        /// Exports a registry key to a string.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <returns>The exported registry key as a string, or null if the operation failed.</returns>
        public string? ExportKey(string keyPath)
        {
            if (!CheckWindowsPlatform())
                return null;

            try
            {
                _logService.Log(LogLevel.Info, $"Exporting registry key: {keyPath}");

                // Create a temporary file to export the registry key to
                string tempFile = Path.GetTempFileName();

                // Export the registry key using reg.exe
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"export \"{keyPath}\" \"{tempFile}\" /y",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    _logService.Log(
                        LogLevel.Error,
                        $"Error exporting registry key {keyPath}: {error}"
                    );
                    return null;
                }

                // Read the exported registry key from the temporary file
                string exportedKey = File.ReadAllText(tempFile);

                // Delete the temporary file
                File.Delete(tempFile);

                return exportedKey;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error exporting registry key {keyPath}: {ex.Message}"
                );
                return null;
            }
        }

        /// <summary>
        /// Imports a registry key from a string.
        /// </summary>
        /// <param name="registryContent">The registry content to import.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public bool ImportKey(string registryContent)
        {
            if (!CheckWindowsPlatform())
                return false;

            try
            {
                _logService.Log(LogLevel.Info, "Importing registry key");

                // Create a temporary file to import the registry key from
                string tempFile = Path.GetTempFileName();

                // Write the registry content to the temporary file
                File.WriteAllText(tempFile, registryContent);

                // Import the registry key using reg.exe
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"import \"{tempFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };

                process.Start();
                process.WaitForExit();

                // Delete the temporary file
                File.Delete(tempFile);

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    _logService.Log(LogLevel.Error, $"Error importing registry key: {error}");
                    return false;
                }

                // No caching - direct registry access only
                _logService.Log(LogLevel.Debug, "Registry key imported successfully");

                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error importing registry key: {ex.Message}");
                return false;
            }
        }
    }
}
