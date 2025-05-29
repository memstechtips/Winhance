using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Text;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    public partial class RegistryService
    {
        // This field is used to track whether a setting is being enabled or disabled
        // It's set in ApplySettingAsync and used in FormatPowerShellCommand
        private bool _lastIsEnabled = true;
        /// <summary>
        /// Sets a registry value using PowerShell with elevated privileges.
        /// This is a fallback method for when the standard .NET Registry API fails.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key.</param>
        /// <param name="valueName">The name of the registry value.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="valueKind">The type of the registry value.</param>
        /// <returns>True if the value was successfully set, false otherwise.</returns>
        private bool SetValueUsingPowerShell(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            try
            {
                _logService.LogInformation($"Attempting to set registry value using PowerShell: {keyPath}\\{valueName}");

                // Format the PowerShell command based on the value type
                string psCommand = FormatPowerShellCommand(keyPath, valueName, value, valueKind);

                // Create a PowerShell process with elevated privileges
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // Run as administrator
                };

                _logService.LogInformation($"Executing PowerShell command: {psCommand}");

                using var process = new Process { StartInfo = startInfo };
                
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Failed to start PowerShell with admin rights: {ex.Message}. Trying without elevation...");
                    
                    // Try again without elevation
                    startInfo.Verb = null;
                    using var nonElevatedProcess = new Process { StartInfo = startInfo };
                    nonElevatedProcess.Start();
                    
                    string nonElevatedOutput = nonElevatedProcess.StandardOutput.ReadToEnd();
                    string nonElevatedError = nonElevatedProcess.StandardError.ReadToEnd();
                    
                    nonElevatedProcess.WaitForExit();
                    
                    if (nonElevatedProcess.ExitCode == 0)
                    {
                        _logService.LogSuccess($"Successfully set registry value using non-elevated PowerShell: {keyPath}\\{valueName}");
                        
                        // Clear the cache for this value
                        string fullValuePath = $"{keyPath}\\{valueName}";
                        lock (_valueCache)
                        {
                            if (_valueCache.ContainsKey(fullValuePath))
                            {
                                _valueCache.Remove(fullValuePath);
                            }
                        }
                        
                        return true;
                    }
                    
                    _logService.LogWarning($"Non-elevated PowerShell also failed: {nonElevatedError}");
                    return false;
                }
                
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    _logService.LogSuccess($"Successfully set registry value using PowerShell: {keyPath}\\{valueName}");
                    
                    // Clear the cache for this value
                    string fullValuePath = $"{keyPath}\\{valueName}";
                    lock (_valueCache)
                    {
                        if (_valueCache.ContainsKey(fullValuePath))
                        {
                            _valueCache.Remove(fullValuePath);
                        }
                    }
                    
                    return true;
                }
                else
                {
                    _logService.LogWarning($"PowerShell failed to set registry value: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error using PowerShell to set registry value: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Formats a PowerShell command to set a registry value.
        /// </summary>
        private string FormatPowerShellCommand(string keyPath, string valueName, object value, RegistryValueKind valueKind)
        {
            // Split the key path into hive and subkey
            string[] pathParts = keyPath.Split('\\');
            string hive = pathParts[0];
            string subKey = string.Join('\\', pathParts.Skip(1));

            // Map the registry hive to PowerShell drive
            string psDrive = hive switch
            {
                "HKCU" or "HKEY_CURRENT_USER" => "HKCU:",
                "HKLM" or "HKEY_LOCAL_MACHINE" => "HKLM:",
                "HKCR" or "HKEY_CLASSES_ROOT" => "HKCR:",
                "HKU" or "HKEY_USERS" => "HKU:",
                "HKCC" or "HKEY_CURRENT_CONFIG" => "HKCC:",
                _ => throw new ArgumentException($"Unsupported registry hive: {hive}")
            };
            
            // Check if this is a GUID subkey operation (for special Explorer items like Home Folder, Gallery, 3D Objects)
            bool isGuidSubkey = valueName.StartsWith("{") && valueName.EndsWith("}") && 
                              (subKey.EndsWith("NameSpace") || subKey.Contains("NameSpace\\"));
            
            if (isGuidSubkey)
            {
                // These are the known GUID values for special Explorer items
                bool isSpecialGuid = valueName == "{f874310e-b6b7-47dc-bc84-b9e6b38f5903}" || // Home Folder
                                  valueName == "{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}" || // Gallery
                                  valueName == "{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}"; // 3D Objects
                
                // Check if we're in the ApplicationSettingItem.ApplyRegistrySettings method
                // If the value is null and we're disabling the setting, we need to remove the key
                if (value == null && !_lastIsEnabled)
                {
                    // When disabling a setting with a null value, remove the GUID subkey
                    _logService.LogInformation($"Using Remove-Item for GUID subkey: {psDrive}\\{subKey}\\{valueName}");
                    return $"Remove-Item -Path '{psDrive}\\{subKey}\\{valueName}' -Force -ErrorAction SilentlyContinue";
                }
                
                // For empty string values, we also need to check if we're disabling
                if (value is string strValue && string.IsNullOrEmpty(strValue) && !_lastIsEnabled)
                {
                    // When disabling a setting with an empty string value, remove the GUID subkey
                    _logService.LogInformation($"Using Remove-Item for GUID subkey with empty string: {psDrive}\\{subKey}\\{valueName}");
                    return $"Remove-Item -Path '{psDrive}\\{subKey}\\{valueName}' -Force -ErrorAction SilentlyContinue";
                }
                
                // For special Explorer items with RegistryValueKind.None, we need to create the key when enabling
                if (valueKind == RegistryValueKind.None && isSpecialGuid)
                {
                    if (_lastIsEnabled)
                    {
                        // When enabling, create the GUID subkey
                        _logService.LogInformation($"Using New-Item for GUID subkey: {psDrive}\\{subKey}\\{valueName}");
                        return $"New-Item -Path '{psDrive}\\{subKey}\\{valueName}' -Force";
                    }
                    else
                    {
                        // When disabling, remove the GUID subkey
                        _logService.LogInformation($"Using Remove-Item for GUID subkey: {psDrive}\\{subKey}\\{valueName}");
                        return $"Remove-Item -Path '{psDrive}\\{subKey}\\{valueName}' -Force -ErrorAction SilentlyContinue";
                    }
                }
                
                // For all other GUID subkey operations, use a simplified command based on the value
                if (value == null || (value is string s && string.IsNullOrEmpty(s)))
                {
                    // For null or empty values, create the GUID subkey
                    return $"New-Item -Path '{psDrive}\\{subKey}\\{valueName}' -Force";
                }
                else
                {
                    // For non-empty values, set the value
                    string valueArg = FormatValueForPowerShell(value, valueKind);
                    string typeArg = GetPowerShellTypeArg(valueKind);
                    return $"New-Item -Path '{psDrive}\\{subKey}\\{valueName}' -Force | Out-Null; Set-ItemProperty -Path '{psDrive}\\{subKey}\\{valueName}' -Name '(Default)' -Value {valueArg} {typeArg} -Force";
                }
            }
            else
            {
                // Format the value based on its type
                string valueArg = FormatValueForPowerShell(value, valueKind);
                string typeArg = GetPowerShellTypeArg(valueKind);

                // Build the PowerShell command with enhanced error handling and permissions
                var sb = new StringBuilder();
                
                // Add error handling
                sb.Append("$ErrorActionPreference = 'Stop'; ");
                sb.Append("try { ");
                
                // First, ensure the key exists with proper permissions
                sb.Append($"if (-not (Test-Path -Path '{psDrive}\\{subKey}')) {{ ");
                
                // Create the key with force to ensure all parent keys are created
                sb.Append($"New-Item -Path '{psDrive}\\{subKey}' -Force | Out-Null; ");
                
                // For policy keys, try to set appropriate permissions
                if (subKey.Contains("Policies", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append($"$acl = Get-Acl -Path '{psDrive}\\{subKey}'; ");
                    sb.Append("$rule = New-Object System.Security.AccessControl.RegistryAccessRule('CURRENT_USER', 'FullControl', 'Allow'); ");
                    sb.Append("$acl.SetAccessRule($rule); ");
                    sb.Append($"try {{ Set-Acl -Path '{psDrive}\\{subKey}' -AclObject $acl }} catch {{ Write-Host 'Could not set ACL, continuing anyway...' }}; ");
                }
                
                sb.Append("} ");
                
                // Then set the value
                sb.Append($"Set-ItemProperty -Path '{psDrive}\\{subKey}' -Name '{valueName}' -Value {valueArg} {typeArg} -Force; ");
                
                // Verify the value was set correctly
                sb.Append($"$setVal = Get-ItemProperty -Path '{psDrive}\\{subKey}' -Name '{valueName}' -ErrorAction SilentlyContinue; ");
                sb.Append("if ($setVal -eq $null) { throw 'Value was not set properly' }; ");
                
                // Close the try block and add catch
                sb.Append("} catch { Write-Error $_.Exception.Message; exit 1 }");

                return sb.ToString();
            }
        }

        /// <summary>
        /// Formats a value for use with PowerShell.
        /// </summary>
        private string FormatValueForPowerShell(object value, RegistryValueKind valueKind)
        {
            return valueKind switch
            {
                RegistryValueKind.DWord or RegistryValueKind.QWord => value.ToString(),
                RegistryValueKind.String or RegistryValueKind.ExpandString => $"'{value}'",
                RegistryValueKind.MultiString => $"@('{string.Join("','", (string[])value)})'",
                RegistryValueKind.Binary => $"([byte[]]@({string.Join(",", (byte[])value)}))",
                _ => $"'{value}'"
            };
        }

        /// <summary>
        /// Gets the PowerShell type argument for a registry value kind.
        /// </summary>
        private string GetPowerShellTypeArg(RegistryValueKind valueKind)
        {
            return valueKind switch
            {
                RegistryValueKind.String => "-Type String",
                RegistryValueKind.ExpandString => "-Type ExpandString",
                RegistryValueKind.Binary => "-Type Binary",
                RegistryValueKind.DWord => "-Type DWord",
                RegistryValueKind.MultiString => "-Type MultiString",
                RegistryValueKind.QWord => "-Type QWord",
                _ => ""
            };
        }
    }
}