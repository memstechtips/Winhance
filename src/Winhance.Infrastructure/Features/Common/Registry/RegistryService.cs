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
    /// - RegistryServicePowerShell.cs - PowerShell fallback methods
    /// - RegistryServiceTestMethods.cs - Testing methods
    /// - RegistryServiceCompletion.cs - Helper methods
    /// - RegistryServiceUtilityOperations.cs - Additional utility operations (export, backup, restore)
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class RegistryService : IRegistryService
    {
        // Cache for registry key existence to avoid repeated checks
        private readonly Dictionary<string, bool> _keyExistsCache = new Dictionary<string, bool>();

        // Cache for registry value existence to avoid repeated checks
        private readonly Dictionary<string, bool> _valueExistsCache =
            new Dictionary<string, bool>();

        // Cache for registry values to avoid repeated reads
        private readonly Dictionary<string, object?> _valueCache =
            new Dictionary<string, object?>();

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

            _logService.Log(LogLevel.Info, "Registry caches cleared");
        }

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
            
            // Convert registry hive to PowerShell drive notation
            string psDrive = setting.Hive switch
            {
                RegistryHive.LocalMachine => "HKLM:",
                RegistryHive.CurrentUser => "HKCU:",
                RegistryHive.ClassesRoot => "HKCR:",
                RegistryHive.Users => "HKU:",
                RegistryHive.CurrentConfig => "HKCC:",
                _ => throw new ArgumentException($"Unsupported registry hive: {setting.Hive}")
            };
            
            if (isEnabled)
            {
                // When enabling, create the GUID subkey
                _logService.Log(LogLevel.Info, $"Creating GUID subkey: {guidSubkeyPath}");
                
                // Use PowerShell directly to create the subkey with a simple command
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"New-Item -Path '{psDrive}\\{setting.SubKey}\\{setting.Name}' -Force\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                process.Start();
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    _logService.Log(
                        LogLevel.Error,
                        $"PowerShell failed to create GUID subkey: {error}"
                    );
                    return false;
                }
                
                _logService.Log(
                    LogLevel.Success,
                    $"Successfully created GUID subkey using PowerShell: {guidSubkeyPath}"
                );
                return true;
            }
            else
            {
                // When disabling, delete the GUID subkey
                _logService.Log(LogLevel.Info, $"Deleting GUID subkey: {guidSubkeyPath}");
                
                // Use PowerShell directly to delete the subkey with a simple command
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path '{psDrive}\\{setting.SubKey}\\{setting.Name}' -Force\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                process.Start();
                await process.WaitForExitAsync();
                
                _logService.Log(
                    LogLevel.Success,
                    $"PowerShell command to delete GUID subkey completed with exit code: {process.ExitCode}"
                );
                
                return true;
            }
        }

        public async Task<bool> ApplySettingAsync(RegistrySetting setting, bool isEnabled)
        {
            if (setting == null)
                return false;

            try
            {
                // Set the _lastIsEnabled field to track whether we're enabling or disabling the setting
                _lastIsEnabled = isEnabled;
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
                        // When enabling a Remove action, we create the key (for special cases like Home Folder)
                        _logService.Log(
                            LogLevel.Info, 
                            $"Creating registry key/value for Remove action: {fullKeyPath}"
                        );
                        
                        // For special cases like navigation pane items, use a simple PowerShell command
                        // to create the key directly
                        string psDrive = setting.Hive switch
                        {
                            RegistryHive.LocalMachine => "HKLM:",
                            RegistryHive.CurrentUser => "HKCU:",
                            RegistryHive.ClassesRoot => "HKCR:",
                            RegistryHive.Users => "HKU:",
                            RegistryHive.CurrentConfig => "HKCC:",
                            _ => throw new ArgumentException($"Unsupported registry hive: {setting.Hive}")
                        };
                        
                        // Use PowerShell to create the key with a simple command
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"New-Item -Path '{psDrive}\\{setting.SubKey}\\{setting.Name}' -Force\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                        
                        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                        process.Start();
                        process.WaitForExit();
                        
                        if (process.ExitCode != 0)
                        {
                            string error = process.StandardError.ReadToEnd();
                            _logService.Log(
                                LogLevel.Error,
                                $"PowerShell failed to create registry key: {error}"
                            );
                            return false;
                        }
                        
                        _logService.Log(
                            LogLevel.Success,
                            $"Successfully created registry key for Remove action: {fullKeyPath}"
                        );
                        return true;
                    }
                    else
                    {
                        // When disabling a Remove action, we delete the key/value
                        _logService.Log(
                            LogLevel.Info, 
                            $"Deleting registry key/value for Remove action: {fullKeyPath}"
                        );
                        
                        // Use PowerShell to remove the key with a simple command
                        string psDrive = setting.Hive switch
                        {
                            RegistryHive.LocalMachine => "HKLM:",
                            RegistryHive.CurrentUser => "HKCU:",
                            RegistryHive.ClassesRoot => "HKCR:",
                            RegistryHive.Users => "HKU:",
                            RegistryHive.CurrentConfig => "HKCC:",
                            _ => throw new ArgumentException($"Unsupported registry hive: {setting.Hive}")
                        };
                        
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Remove-Item -Path '{psDrive}\\{setting.SubKey}\\{setting.Name}' -Force -ErrorAction SilentlyContinue\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                        
                        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                        process.Start();
                        process.WaitForExit();
                        
                        _logService.Log(
                            LogLevel.Success,
                            $"PowerShell command to remove registry key completed with exit code: {process.ExitCode}"
                        );
                        
                        return true;
                    }
                }
                
                // Standard handling for other action types
                object? valueToSet = null;

                if (isEnabled)
                {
                    // When enabling, use EnabledValue if available, otherwise fall back to RecommendedValue
                    valueToSet = setting.EnabledValue ?? setting.RecommendedValue;
                    _logService.Log(
                        LogLevel.Debug,
                        $"Setting {setting.Name} - EnabledValue: {setting.EnabledValue}, RecommendedValue: {setting.RecommendedValue}, Using: {valueToSet}"
                    );
                }
                else
                {
                    // When disabling, use DisabledValue if available, otherwise fall back to DefaultValue
                    valueToSet = setting.DisabledValue ?? setting.DefaultValue;
                    _logService.Log(
                        LogLevel.Debug,
                        $"Setting {setting.Name} - DisabledValue: {setting.DisabledValue}, DefaultValue: {setting.DefaultValue}, Using: {valueToSet}"
                    );
                }

                if (valueToSet == null)
                {
                    // If the value to set is null, delete the value
                    _logService.Log(
                        LogLevel.Warning,
                        $"Value to set for {setting.Name} is null, deleting the value"
                    );
                    return await DeleteValue(setting.Hive, setting.SubKey, setting.Name);
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
                        _logService.Log(
                            LogLevel.Warning,
                            $"Failed to create registry key: {keyPath}, attempting PowerShell fallback"
                        );
                        
                        // Try to use PowerShell to create the key if direct creation failed
                        if (keyPath.Contains("Policies", StringComparison.OrdinalIgnoreCase))
                        {
                            _logService.Log(
                                LogLevel.Info,
                                $"Attempting to create policy registry key using PowerShell: {keyPath}"
                            );
                            
                            // Use SetValueUsingPowerShell which will create the key as part of setting the value
                            return SetValueUsingPowerShell(keyPath, setting.Name, valueToSet, setting.ValueType);
                        }
                        
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
                            _logService.Log(
                                LogLevel.Warning,
                                $"Value verification failed - value is null after setting: {keyPath}\\{setting.Name}"
                            );
                            
                            // Try one more time with PowerShell
                            return SetValueUsingPowerShell(keyPath, setting.Name, valueToSet, setting.ValueType);
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
                    _logService.Log(
                        LogLevel.Debug,
                        $"Processing linked setting {settingCount}/{totalSettings}: {setting.Name}"
                    );

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

                // Clear all caches to ensure fresh reads
                ClearRegistryCaches();

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
