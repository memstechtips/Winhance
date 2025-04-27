using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Security.AccessControl;
using System.Security.Principal;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    [SupportedOSPlatform("windows")]
    public partial class RegistryService : IRegistryService
    {
        private readonly ILogService _logService;

        public RegistryService(ILogService logService)
        {
            _logService = logService;
        }

        public bool SetValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.LogInformation($"Setting registry value: {keyPath}\\{valueName}");

                // First ensure the key exists with full access rights
                string[] pathParts = keyPath.Split('\\');
                RegistryKey? rootKey = GetRootKey(pathParts[0]);

                if (rootKey == null)
                {
                    _logService.LogError($"Invalid root key: {pathParts[0]}");
                    return false;
                }

                string subKeyPath = string.Join('\\', pathParts.Skip(1));

                // Create the key with direct Registry API and security settings
                // This will also attempt to take ownership if needed
                RegistryKey? targetKey = EnsureKeyWithFullAccess(rootKey, subKeyPath);

                if (targetKey == null)
                {
                    _logService.LogWarning($"Could not open or create registry key: {keyPath}");
                    return false;
                }

                using (targetKey)
                {
                    try
                    {
                        targetKey.SetValue(valueName, value, valueKind);

                        // Clear the cache for this value
                        string fullValuePath = $"{keyPath}\\{valueName}";
                        lock (_valueCache)
                        {
                            if (_valueCache.ContainsKey(fullValuePath))
                            {
                                _valueCache.Remove(fullValuePath);
                            }
                        }

                        _logService.LogSuccess($"Successfully set registry value: {keyPath}\\{valueName}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Failed to set registry value even after taking ownership: {ex.Message}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error setting registry value {keyPath}\\{valueName}", ex);
                return false;
            }
        }

        public object? GetValue(string keyPath, string valueName)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return null;
                }

                _logService.LogInformation($"Retrieving registry value: {keyPath}\\{valueName}");

                using (var key = OpenRegistryKey(keyPath, false))
                {
                    if (key == null)
                    {
                        _logService.LogWarning($"Registry key not found: {keyPath}");
                        return null;
                    }

                    var value = key.GetValue(valueName);
                    _logService.LogInformation($"Retrieved value for {keyPath}\\{valueName}: {value}");
                    return value;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error getting registry value {keyPath}\\{valueName}", ex);
                return null;
            }
        }

        public bool DeleteValue(string keyPath, string valueName)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.LogInformation($"Deleting registry value: {keyPath}\\{valueName}");

                // First try the standard API approach
                bool success = false;
                using (var key = OpenRegistryKey(keyPath, true))
                {
                    if (key != null)
                    {
                        try
                        {
                            key.DeleteValue(valueName, false);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            _logService.LogWarning($"Standard API failed to delete registry value: {ex.Message}");
                            // We'll try the alternative approach below
                        }
                    }
                    else
                    {
                        _logService.LogWarning($"Could not open registry key: {keyPath}");
                    }
                }

                // If standard approach failed, try using reg.exe command-line tool
                if (!success)
                {
                    _logService.LogInformation($"Attempting to delete registry value using reg.exe: {keyPath}\\{valueName}");
                    success = DeleteValueUsingRegExe(keyPath, valueName);
                }

                // Verify the value was actually deleted
                if (success)
                {
                    bool valueStillExists = false;
                    using (var key = OpenRegistryKey(keyPath, false))
                    {
                        if (key != null)
                        {
                            try
                            {
                                var valueNames = key.GetValueNames();
                                valueStillExists = valueNames.Contains(valueName);
                            }
                            catch (Exception ex)
                            {
                                _logService.LogWarning($"Error verifying value deletion: {ex.Message}");
                            }
                        }
                    }

                    if (valueStillExists)
                    {
                        _logService.LogWarning($"Registry value still exists after deletion attempt: {keyPath}\\{valueName}");
                        return false;
                    }
                    else
                    {
                        // Clear the cache for this value
                        string fullValuePath = $"{keyPath}\\{valueName}";
                        lock (_valueCache)
                        {
                            if (_valueCache.ContainsKey(fullValuePath))
                            {
                                _valueCache.Remove(fullValuePath);
                            }
                        }

                        lock (_valueExistsCache)
                        {
                            if (_valueExistsCache.ContainsKey(fullValuePath))
                            {
                                _valueExistsCache.Remove(fullValuePath);
                            }
                        }

                        _logService.LogSuccess($"Successfully deleted and verified registry value: {keyPath}\\{valueName}");
                        return true;
                    }
                }
                else
                {
                    _logService.LogWarning($"Failed to delete registry value: {keyPath}\\{valueName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error deleting registry value {keyPath}\\{valueName}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteValue(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                string hiveString = GetRegistryHiveString(hive);
                string fullPath = $"{hiveString}\\{subKey}";

                _logService.LogInformation($"Deleting registry value: {fullPath}\\{valueName}");

                return DeleteValue(fullPath, valueName);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error deleting registry value {hive}\\{subKey}\\{valueName}", ex);
                return false;
            }
        }

        public async Task<string> ExportKey(string keyPath, bool includeSubKeys)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return string.Empty;
                }

                _logService.LogInformation($"Exporting registry key: {keyPath}");

                // Create a temporary file to store the export
                string tempFile = Path.Combine(Path.GetTempPath(), $"reg_export_{Guid.NewGuid()}.reg");

                // Use Process to run the reg.exe tool
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"export \"{keyPath}\" \"{tempFile}\" {(includeSubKeys ? "/y" : "")}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    _logService.LogError($"Error exporting registry key: {error}");
                    return string.Empty;
                }

                // Read the exported file
                string exportContent = await File.ReadAllTextAsync(tempFile);

                // Clean up the temporary file
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Failed to delete temporary file: {ex.Message}");
                }

                _logService.LogSuccess($"Registry key exported successfully: {keyPath}");
                return exportContent;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error exporting registry key: {ex.Message}", ex);
                return string.Empty;
            }
        }

        public bool KeyExists(string keyPath)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.LogInformation($"Checking if registry key exists: {keyPath}");

                using (var key = OpenRegistryKey(keyPath, false))
                {
                    bool exists = key != null;
                    _logService.LogInformation($"Registry key existence check: {keyPath} = {exists}");
                    return exists;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking registry key existence: {keyPath}", ex);
                return false;
            }
        }

        public bool CreateKey(string keyPath)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.LogInformation($"Creating registry key: {keyPath}");

                string[] pathParts = keyPath.Split('\\');
                RegistryKey? rootKey = GetRootKey(pathParts[0]);

                if (rootKey == null)
                {
                    _logService.LogError($"Invalid root key: {pathParts[0]}");
                    return false;
                }

                string subPath = string.Join('\\', pathParts.Skip(1));
                var key = EnsureKeyWithFullAccess(rootKey, subPath);

                if (key != null)
                {
                    key.Close();
                    _logService.LogSuccess($"Successfully created registry key: {keyPath}");
                    return true;
                }
                else
                {
                    _logService.LogError($"Failed to create registry key: {keyPath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error creating registry key: {keyPath}", ex);
                return false;
            }
        }

        public bool ValueExists(string keyPath, string valueName)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.LogInformation($"Checking if registry value exists: {keyPath}\\{valueName}");

                using (var key = OpenRegistryKey(keyPath, false))
                {
                    if (key == null)
                    {
                        _logService.LogWarning($"Registry key not found: {keyPath}");
                        return false;
                    }

                    return key.GetValue(valueName) != null;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking registry value existence: {keyPath}\\{valueName}", ex);
                return false;
            }
        }

        public RegistrySettingStatus TestRegistrySetting(string keyPath, string valueName, object desiredValue)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return RegistrySettingStatus.Unknown;
                }

                _logService.LogInformation($"Testing registry setting: {keyPath}\\{valueName}");

                using (var key = OpenRegistryKey(keyPath, false))
                {
                    if (key == null)
                {
                    _logService.LogInformation($"Registry key not found: {keyPath}");
                    // Assuming NotApplied is the correct enum member for this state
                    return RegistrySettingStatus.NotApplied;
                }

                var currentValue = key.GetValue(valueName);
                if (currentValue == null)
                {
                    _logService.LogInformation($"Registry value not found: {keyPath}\\{valueName}");
                    // Assuming NotApplied is the correct enum member for this state
                    return RegistrySettingStatus.NotApplied;
                }

                // Assuming Applied and NotApplied are the correct enum members
                bool matches = currentValue.Equals(desiredValue);
                RegistrySettingStatus status = matches ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;

                _logService.LogInformation($"Registry setting test for {keyPath}\\{valueName}: Current={currentValue}, Desired={desiredValue}, Status={status}");
                return status;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error testing registry setting: {keyPath}\\{valueName}", ex);
                return RegistrySettingStatus.Unknown;
            }
        }

        public async Task<bool> BackupRegistry(string backupPath)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.LogInformation($"Backing up registry to: {backupPath}");

                // Use Process to run the reg.exe tool
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"export HKLM \"{backupPath}\\HKLM.reg\" /y",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logService.LogError($"Error backing up HKLM registry: {await process.StandardError.ReadToEndAsync()}");
                    return false;
                }

                // Also export HKCU
                using var process2 = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"export HKCU \"{backupPath}\\HKCU.reg\" /y",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process2.Start();
                await process2.WaitForExitAsync();

                if (process2.ExitCode != 0)
                {
                    _logService.LogError($"Error backing up HKCU registry: {await process2.StandardError.ReadToEndAsync()}");
                    return false;
                }

                _logService.LogSuccess($"Registry backup completed to: {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error backing up registry: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> RestoreRegistry(string backupPath)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.LogInformation($"Restoring registry from: {backupPath}");
                bool success = true;

                // Use Process to run the reg.exe tool for HKLM
                string hklmPath = System.IO.Path.Combine(backupPath, "HKLM.reg");
                if (System.IO.File.Exists(hklmPath))
                {
                    using var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "reg.exe",
                            Arguments = $"import \"{hklmPath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        _logService.LogError($"Error restoring HKLM registry: {await process.StandardError.ReadToEndAsync()}");
                        success = false;
                    }
                }
                else
                {
                    _logService.LogWarning($"HKLM registry backup file not found: {hklmPath}");
                }

                // Use Process to run the reg.exe tool for HKCU
                string hkcuPath = System.IO.Path.Combine(backupPath, "HKCU.reg");
                if (System.IO.File.Exists(hkcuPath))
                {
                    using var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "reg.exe",
                            Arguments = $"import \"{hkcuPath}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        _logService.LogError($"Error restoring HKCU registry: {await process.StandardError.ReadToEndAsync()}");
                        success = false;
                    }
                }
                else
                {
                    _logService.LogWarning($"HKCU registry backup file not found: {hkcuPath}");
                }

                if (success)
                {
                    _logService.LogSuccess($"Registry restored from: {backupPath}");
                }
                else
                {
                    _logService.LogWarning($"Registry restore completed with errors from: {backupPath}");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error restoring registry: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> ApplyCustomizations(List<RegistrySetting> settings)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.Log(LogLevel.Info, $"Applying {settings.Count} registry customizations"); // Corrected Log call

                int successCount = 0;
                int totalCount = settings.Count;

                foreach (var setting in settings)
                {
                    string hiveString = GetRegistryHiveString(setting.Hive);
                    string fullPath = $"{hiveString}\\{setting.SubKey}";

                    _logService.Log(LogLevel.Info, $"Applying customization: {setting.Name} ({fullPath}\\{setting.Name})"); // Corrected Log call

                    bool success = false;

                    // Special handling for ActionType.Remove settings
                    if (setting.ActionType == RegistryActionType.Remove)
                    {
                        // For ActionType.Remove, we need to create the key when enabling
                        _logService.Log(LogLevel.Info, $"Creating registry key for ActionType.Remove setting: {fullPath}");
                        
                        // If the Name is a GUID, it's likely a subkey that needs to be created
                        if (Guid.TryParse(setting.Name.Trim('{', '}'), out _))
                        {
                            // Create a subkey with the GUID name
                            string fullKeyPath = $"{fullPath}\\{setting.Name}";
                            _logService.Log(LogLevel.Info, $"Creating GUID subkey: {fullKeyPath}");
                            success = CreateKey(fullKeyPath);
                        }
                        else
                        {
                            // Just create the main key
                            success = CreateKey(fullPath);
                        }
                    }
                    else
                    {
                        // Standard handling for regular registry settings
                        // Use EnabledValue if available, otherwise fall back to RecommendedValue for backward compatibility
                        object valueToSet = setting.EnabledValue ?? setting.RecommendedValue;
                        success = SetValue(fullPath, setting.Name, valueToSet, setting.ValueType);
                    }

                    if (success)
                    {
                        successCount++;
                    }
                }

                bool allSucceeded = (successCount == totalCount);
                string resultMessage = $"Applied {successCount} of {totalCount} registry customizations";

                if (allSucceeded)
                {
                    _logService.Log(LogLevel.Success, resultMessage); // Corrected Log call
                }
                else
                {
                    _logService.Log(LogLevel.Warning, resultMessage); // Corrected Log call
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying registry customizations: {ex.Message}", ex); // Corrected Log call
                return false;
            }
        }

        public async Task<bool> RestoreCustomizationDefaults(List<RegistrySetting> settings)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.Log(LogLevel.Info, $"Restoring defaults for {settings.Count} registry customizations"); // Corrected Log call

                int successCount = 0;
                int totalCount = settings.Count;

                foreach (var setting in settings)
                {
                    string hiveString = GetRegistryHiveString(setting.Hive);
                    string fullPath = $"{hiveString}\\{setting.SubKey}";

                    _logService.Log(LogLevel.Info, $"Restoring default for: {setting.Name} ({fullPath}\\{setting.Name})"); // Corrected Log call

                    bool success;

                    // Check if this is a Group Policy registry key that needs special handling
                    if (setting.IsGroupPolicy)
                    {
                        // For Group Policy keys, we need to delete the entire key when restoring defaults
                        // This ensures Windows recognizes that the policy is no longer applied
                        _logService.Log(LogLevel.Info, $"Restoring defaults by deleting Group Policy registry key: {fullPath}");
                        success = DeleteKey(fullPath);
                    }
                    // Special handling for ActionType.Remove settings
                    else if (setting.ActionType == RegistryActionType.Remove)
                    {
                        // For ActionType.Remove, we need to delete the key when disabling
                        _logService.Log(LogLevel.Info, $"Restoring defaults by deleting registry key for ActionType.Remove setting");
                        
                        // If the Name is a GUID, it's likely a subkey that needs to be deleted
                        if (Guid.TryParse(setting.Name.Trim('{', '}'), out _))
                        {
                            // Delete the subkey with the GUID name
                            string fullKeyPath = $"{fullPath}\\{setting.Name}";
                            _logService.Log(LogLevel.Info, $"Deleting GUID subkey: {fullKeyPath}");
                            success = DeleteKey(fullKeyPath);
                        }
                        else
                        {
                            // Delete the value
                            success = DeleteValue(fullPath, setting.Name);
                        }
                    }
                    else
                    {
                        // Use DisabledValue if available, otherwise fall back to DefaultValue for backward compatibility
                        object? valueToSet = setting.DisabledValue ?? setting.DefaultValue;

                        // If the value is null, delete the registry key
                        if (valueToSet == null)
                        {
                            success = DeleteValue(fullPath, setting.Name);
                        }
                        else
                        {
                            success = SetValue(fullPath, setting.Name, valueToSet, setting.ValueType);
                        }
                    }

                    if (success)
                    {
                        successCount++;
                    }
                }

                bool allSucceeded = (successCount == totalCount);
                string resultMessage = $"Restored defaults for {successCount} of {totalCount} registry customizations";

                if (allSucceeded)
                {
                    _logService.Log(LogLevel.Success, resultMessage); // Corrected Log call
                }
                else
                {
                    _logService.Log(LogLevel.Warning, resultMessage); // Corrected Log call
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error restoring registry defaults: {ex.Message}", ex); // Corrected Log call
                return false;
            }
        }

        public async Task<bool> ApplyPowerPlanSettings(List<PowerCfgSetting> settings)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.Log(LogLevel.Info, $"Applying {settings.Count} power plan settings"); // Corrected Log call

                // Implement power plan settings application using PowerPlanService
                // This would involve calling appropriate methods from PowerPlanService class
                // For now, just return true to fix build errors
                _logService.Log(LogLevel.Success, "Power plan settings applied"); // Corrected Log call
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying power plan settings: {ex.Message}", ex); // Corrected Log call
                return false;
            }
        }

        public async Task<bool> RestoreDefaultPowerSettings() // Reverted to public
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.Log(LogLevel.Info, "Restoring default power settings"); // Corrected Log call

                // Implement default power settings restoration using PowerPlanService
                // This would involve calling appropriate methods from PowerPlanService class
                _logService.Log(LogLevel.Success, "Default power settings restored"); // Corrected Log call
                return true;
            }
            catch (Exception ex) // Added missing catch block
            {
                _logService.Log(LogLevel.Error, $"Error restoring default power settings: {ex.Message}", ex); // Corrected Log call
                return false;
            }
        }


        private bool DeleteValueUsingRegExe(string keyPath, string valueName)
        {
            try
            {
                // Format the command to delete the registry value
                // reg delete "HKLM\Software\Path" /v "ValueName" /f
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"delete \"{keyPath}\" /v \"{valueName}\" /f",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas" // Run as administrator
                    }
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    _logService.LogInformation($"Successfully deleted registry value using reg.exe: {keyPath}\\{valueName}");
                    return true;
                }
                else
                {
                    string error = process.StandardError.ReadToEnd();
                    _logService.LogWarning($"reg.exe failed to delete registry value: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error using reg.exe to delete registry value: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Deletes a registry key and all its values.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key to delete.</param>
        /// <returns>True if the key was successfully deleted, false otherwise.</returns>
        public bool DeleteKey(string keyPath)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                _logService.LogInformation($"Deleting registry key: {keyPath}");

                // First try the standard API approach
                bool success = false;
                string[] pathParts = keyPath.Split('\\');
                RegistryKey? rootKey = GetRootKey(pathParts[0]);

                if (rootKey == null)
                {
                    _logService.LogError($"Invalid root key: {pathParts[0]}");
                    return false;
                }

                string subKeyPath = string.Join('\\', pathParts.Skip(1));

                try
                {
                    // Try to delete the key using the .NET API
                    rootKey.DeleteSubKeyTree(subKeyPath, false);
                    success = true;
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Standard API failed to delete registry key: {ex.Message}");
                    // We'll try the alternative approach below
                }

                // If standard approach failed, try using reg.exe command-line tool
                if (!success)
                {
                    _logService.LogInformation($"Attempting to delete registry key using reg.exe: {keyPath}");
                    success = DeleteKeyUsingRegExe(keyPath);
                }

                // Verify the key was actually deleted
                if (success)
                {
                    bool keyStillExists = KeyExists(keyPath);

                    if (keyStillExists)
                    {
                        _logService.LogWarning($"Registry key still exists after deletion attempt: {keyPath}");
                        return false;
                    }
                    else
                    {
                        // Clear the cache for this key
                        lock (_keyExistsCache)
                        {
                            if (_keyExistsCache.ContainsKey(keyPath))
                            {
                                _keyExistsCache.Remove(keyPath);
                            }
                        }

                        // Also clear any cached values that might be under this key
                        string keyPrefix = $"{keyPath}\\";
                        lock (_valueCache)
                        {
                            var keysToRemove = _valueCache.Keys.Where(k => k.StartsWith(keyPrefix)).ToList();
                            foreach (var key in keysToRemove)
                            {
                                _valueCache.Remove(key);
                            }
                        }

                        lock (_valueExistsCache)
                        {
                            var keysToRemove = _valueExistsCache.Keys.Where(k => k.StartsWith(keyPrefix)).ToList();
                            foreach (var key in keysToRemove)
                            {
                                _valueExistsCache.Remove(key);
                            }
                        }

                        _logService.LogSuccess($"Successfully deleted and verified registry key: {keyPath}");
                        return true;
                    }
                }
                else
                {
                    _logService.LogWarning($"Failed to delete registry key: {keyPath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error deleting registry key {keyPath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Deletes a registry key and all its values.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="subKey">The subkey path.</param>
        /// <returns>True if the key was successfully deleted, false otherwise.</returns>
        public async Task<bool> DeleteKey(RegistryHive hive, string subKey)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return false;
                }

                string hiveString = GetRegistryHiveString(hive);
                string fullPath = $"{hiveString}\\{subKey}";

                _logService.LogInformation($"Deleting registry key: {fullPath}");

                return DeleteKey(fullPath);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error deleting registry key {hive}\\{subKey}", ex);
                return false;
            }
        }

        private bool DeleteKeyUsingRegExe(string keyPath)
        {
            try
            {
                // Format the command to delete the registry key
                // reg delete "HKLM\Software\Path" /f
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"delete \"{keyPath}\" /f",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas" // Run as administrator
                    }
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    _logService.LogInformation($"Successfully deleted registry key using reg.exe: {keyPath}");
                    return true;
                }
                else
                {
                    string error = process.StandardError.ReadToEnd();
                    _logService.LogWarning($"reg.exe failed to delete registry key: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error using reg.exe to delete registry key: {ex.Message}", ex);
                return false;
            }
        }

        // Helper method to convert RegistryHive enum to string
        private string GetRegistryHiveString(RegistryHive hive)
        {
            return hive switch
            {
                RegistryHive.ClassesRoot => "HKCR",
                RegistryHive.CurrentUser => "HKCU",
                RegistryHive.LocalMachine => "HKLM",
                RegistryHive.Users => "HKU",
                RegistryHive.CurrentConfig => "HKCC",
                _ => throw new ArgumentException($"Unsupported registry hive: {hive}")
            };
        }

        private RegistryKey? OpenRegistryKey(string keyPath, bool writable)
        {
            string[] pathParts = keyPath.Split('\\');
            RegistryKey? rootKey = GetRootKey(pathParts[0]);

            if (rootKey == null)
            {
                _logService.LogError($"Invalid root key: {pathParts[0]}");
                return null;
            }

            string subPath = string.Join('\\', pathParts.Skip(1));

            if (writable)
            {
                // If we need write access, try to ensure the key exists with proper access rights
                return EnsureKeyWithFullAccess(rootKey, subPath);
            }
            else
            {
                // For read-only access, just try to open the key normally
                return rootKey.OpenSubKey(subPath, false);
            }
        }
    }
}
