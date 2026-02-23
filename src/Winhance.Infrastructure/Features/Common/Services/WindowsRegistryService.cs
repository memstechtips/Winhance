using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsRegistryService(ILogService logService, IInteractiveUserService interactiveUserService) : IWindowsRegistryService
    {
        private bool CreateKey(string keyPath)
        {
            try
            {
                if (KeyExists(keyPath))
                    return true;

                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var createdKey = rootKey.CreateSubKey(subKeyPath, true);
                return createdKey != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SetValue(
            string keyPath,
            string valueName,
            object value,
            RegistryValueKind valueKind
        )
        {
            try
            {
                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var targetKey = rootKey.CreateSubKey(subKeyPath, true);
                if (targetKey == null)
                    return false;

                targetKey.SetValue(valueName, value, valueKind);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public object? GetValue(string keyPath, string valueName)
        {
            try
            {
                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, false);
                return key?.GetValue(valueName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool DeleteKey(string keyPath)
        {
            try
            {
                if (!KeyExists(keyPath))
                    return true;

                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                rootKey.DeleteSubKeyTree(subKeyPath, false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool DeleteValue(string keyPath, string valueName)
        {
            try
            {
                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, true);
                if (key == null)
                    return false;

                key.DeleteValue(valueName, false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool KeyExists(string keyPath)
        {
            try
            {
                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, false);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        public bool ValueExists(string keyPath, string valueName)
        {
            try
            {
                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, false);
                if (key == null)
                    return false;

                return key.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public string[] GetSubKeyNames(string keyPath)
        {
            try
            {
                var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
                using var key = rootKey.OpenSubKey(subKeyPath, false);
                return key?.GetSubKeyNames() ?? Array.Empty<string>();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        public bool RegistryValueExists(RegistrySetting setting)
        {
            try
            {
                if (setting?.ValueName == null)
                    return KeyExists(setting?.KeyPath ?? "");

                if (!KeyExists(setting.KeyPath))
                    return false;

                return ValueExists(setting.KeyPath, setting.ValueName);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error checking registry value existence for {setting?.KeyPath}\\{setting?.ValueName}: {ex.Message}");
                return false;
            }
        }

        public bool IsSettingApplied(RegistrySetting setting)
        {
            try
            {
                if (setting == null)
                    return false;

                if (setting.ApplyPerNetworkInterface)
                {
                    var subKeys = GetSubKeyNames(setting.KeyPath);
                    if (subKeys.Length == 0)
                        return false;

                    foreach (var subKey in subKeys)
                    {
                        var expandedSetting = setting with
                        {
                            KeyPath = $@"{setting.KeyPath}\{subKey}",
                            ApplyPerNetworkInterface = false
                        };
                        if (!IsSettingApplied(expandedSetting))
                            return false;
                    }
                    return true;
                }

                // For settings that check the (Default) value with ValueName = null,
                // we need to check if both EnabledValue and DisabledValue are null.
                // If both are null, we're just checking key existence.
                // If either has a value, we need to read and compare the default value.
                if (setting.ValueName == null && setting.EnabledValue == null && setting.DisabledValue == null)
                {
                    return KeyExists(setting.KeyPath);
                }

                if (!KeyExists(setting.KeyPath))
                {
                    if (setting.CompositeStringKey != null)
                    {
                        // Key absent (fresh Windows) — use DefaultValue to determine implied state
                        var defaultStr = setting.DefaultValue?.ToString();
                        var enabledStr = setting.EnabledValue?.ToString();
                        return string.Equals(defaultStr, enabledStr, StringComparison.OrdinalIgnoreCase);
                    }
                    return setting.EnabledValue == null;
                }

                if (setting.CompositeStringKey != null)
                {
                    var compositeStr = ValueExists(setting.KeyPath, setting.ValueName!)
                        ? (GetValue(setting.KeyPath, setting.ValueName!)?.ToString() ?? "")
                        : "";

                    var pairs = ParseCompositeString(compositeStr);

                    if (pairs.TryGetValue(setting.CompositeStringKey, out var subValue))
                    {
                        var enabledStr = setting.EnabledValue?.ToString();
                        return string.Equals(subValue, enabledStr, StringComparison.OrdinalIgnoreCase);
                    }

                    // Sub-key absent from string — use DefaultValue
                    var defaultStr2 = setting.DefaultValue?.ToString();
                    var enabledStr2 = setting.EnabledValue?.ToString();
                    return string.Equals(defaultStr2, enabledStr2, StringComparison.OrdinalIgnoreCase);
                }

                if (!ValueExists(setting.KeyPath, setting.ValueName!))
                {
                    return setting.EnabledValue == null;
                }

                if (setting.BitMask.HasValue && setting.BinaryByteIndex.HasValue)
                {
                    return IsBitSet(setting.KeyPath, setting.ValueName!, setting.BinaryByteIndex.Value, setting.BitMask.Value);
                }

                if (setting.ModifyByteOnly && setting.BinaryByteIndex.HasValue)
                {
                    var currentByte = GetBinaryByte(setting.KeyPath, setting.ValueName!, setting.BinaryByteIndex.Value);
                    if (currentByte == null)
                        return false;

                    var enabledByte = setting.EnabledValue switch
                    {
                        byte b => b,
                        int i => (byte)i,
                        _ => (byte)0
                    };

                    return currentByte.Value == enabledByte;
                }

                var currentValue = GetValue(setting.KeyPath, setting.ValueName!);

                // When EnabledValue is null, absence is the enabled state.
                // If value exists but doesn't match DisabledValue, treat as enabled.
                if (setting.EnabledValue == null)
                {
                    if (setting.DisabledValue != null && CompareValues(currentValue, setting.DisabledValue))
                        return false;
                    return true; // Value exists but isn't disabled → enabled
                }

                if (CompareValues(currentValue, setting.EnabledValue))
                    return true;

                if (setting.DisabledValue != null && CompareValues(currentValue, setting.DisabledValue))
                    return false;

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte newValue)
        {
            try
            {
                var currentValue = GetValue(keyPath, valueName);
                if (currentValue is not byte[] currentBytes)
                {
                    var defaultBinary = new byte[Math.Max(12, byteIndex + 1)];
                    defaultBinary[byteIndex] = newValue;
                    return SetValue(keyPath, valueName, defaultBinary, RegistryValueKind.Binary);
                }

                if (currentBytes.Length <= byteIndex)
                {
                    var expandedBytes = new byte[byteIndex + 1];
                    Array.Copy(currentBytes, expandedBytes, currentBytes.Length);
                    expandedBytes[byteIndex] = newValue;
                    return SetValue(keyPath, valueName, expandedBytes, RegistryValueKind.Binary);
                }

                var modifiedBytes = (byte[])currentBytes.Clone();
                modifiedBytes[byteIndex] = newValue;

                return SetValue(keyPath, valueName, modifiedBytes, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error modifying byte at index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
        }

        private byte? GetBinaryByte(string keyPath, string valueName, int byteIndex)
        {
            try
            {
                var currentValue = GetValue(keyPath, valueName);
                if (currentValue is byte[] currentBytes && currentBytes.Length > byteIndex)
                {
                    return currentBytes[byteIndex];
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit)
        {
            try
            {
                var currentValue = GetValue(keyPath, valueName);
                if (currentValue is not byte[] currentBytes)
                {
                    var defaultBinary = new byte[Math.Max(12, byteIndex + 1)];
                    defaultBinary[byteIndex] = setBit ? bitMask : (byte)0;
                    return SetValue(keyPath, valueName, defaultBinary, RegistryValueKind.Binary);
                }

                if (currentBytes.Length <= byteIndex)
                {
                    var expandedBytes = new byte[byteIndex + 1];
                    Array.Copy(currentBytes, expandedBytes, currentBytes.Length);
                    expandedBytes[byteIndex] = setBit ? bitMask : (byte)0;
                    return SetValue(keyPath, valueName, expandedBytes, RegistryValueKind.Binary);
                }

                var modifiedBytes = (byte[])currentBytes.Clone();
                if (setBit)
                    modifiedBytes[byteIndex] |= bitMask;
                else
                    modifiedBytes[byteIndex] &= (byte)~bitMask;

                return SetValue(keyPath, valueName, modifiedBytes, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error modifying bit mask 0x{bitMask:X2} at byte index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
                return false;
            }
        }

        private bool IsBitSet(string keyPath, string valueName, int byteIndex, byte bitMask)
        {
            try
            {
                var currentByte = GetBinaryByte(keyPath, valueName, byteIndex);
                if (!currentByte.HasValue)
                    return false;

                return (currentByte.Value & bitMask) == bitMask;
            }
            catch
            {
                return false;
            }
        }

        public bool ApplySetting(RegistrySetting setting, bool isEnabled, object? specificValue = null)
        {
            if (setting == null)
                return false;

            try
            {
                if (setting.ApplyPerNetworkInterface)
                {
                    var subKeys = GetSubKeyNames(setting.KeyPath);
                    if (subKeys.Length == 0)
                    {
                        logService.Log(LogLevel.Warning, $"[WindowsRegistryService] No subkeys found under '{setting.KeyPath}' for per-interface setting");
                        return false;
                    }

                    var allSucceeded = true;
                    foreach (var subKey in subKeys)
                    {
                        var expandedSetting = setting with
                        {
                            KeyPath = $@"{setting.KeyPath}\{subKey}",
                            ApplyPerNetworkInterface = false
                        };
                        if (!ApplySetting(expandedSetting, isEnabled, specificValue))
                            allSucceeded = false;
                    }
                    return allSucceeded;
                }

                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Applying registry setting - Path: {setting.KeyPath}, Value: {setting.ValueName}, Enabled: {isEnabled}");

                if (setting.ValueName == null)
                {
                    var result = isEnabled ? CreateKey(setting.KeyPath) : DeleteKey(setting.KeyPath);
                    return result;
                }

                if (setting.CompositeStringKey != null)
                {
                    if (!CreateKey(setting.KeyPath))
                        return false;

                    var currentComposite = ValueExists(setting.KeyPath, setting.ValueName)
                        ? (GetValue(setting.KeyPath, setting.ValueName)?.ToString() ?? "")
                        : "";

                    var pairs = ParseCompositeString(currentComposite);
                    var newSubValue = specificValue?.ToString()
                        ?? (isEnabled ? setting.EnabledValue?.ToString() : setting.DisabledValue?.ToString());

                    if (newSubValue != null)
                        pairs[setting.CompositeStringKey] = newSubValue;
                    else
                        pairs.Remove(setting.CompositeStringKey);

                    var mergedValue = BuildCompositeString(pairs);
                    var compositeResult = SetValue(setting.KeyPath, setting.ValueName, mergedValue, RegistryValueKind.String);

                    logService.Log(LogLevel.Info,
                        $"[WindowsRegistryService] Updated composite key '{setting.CompositeStringKey}' to '{newSubValue}' in '{setting.KeyPath}\\{setting.ValueName}' - Full value: '{mergedValue}' - Success: {compositeResult}");
                    return compositeResult;
                }

                if (setting.BitMask.HasValue && setting.BinaryByteIndex.HasValue)
                {
                    if (!CreateKey(setting.KeyPath))
                        return false;

                    var result = ModifyBinaryBit(setting.KeyPath, setting.ValueName, setting.BinaryByteIndex.Value, setting.BitMask.Value, isEnabled);
                    logService.Log(LogLevel.Info, $"[WindowsRegistryService] Modified bit mask 0x{setting.BitMask.Value:X2} at byte index {setting.BinaryByteIndex.Value} to {isEnabled} - Success: {result}");
                    return result;
                }

                if (setting.ModifyByteOnly && setting.BinaryByteIndex.HasValue)
                {
                    var byteValue = specificValue switch
                    {
                        byte b => b,
                        int i => (byte)i,
                        _ when isEnabled => setting.EnabledValue switch
                        {
                            byte b => b,
                            int i => (byte)i,
                            _ => (byte)0
                        },
                        _ => setting.DisabledValue switch
                        {
                            byte b => b,
                            int i => (byte)i,
                            _ => (byte)0
                        }
                    };

                    if (!CreateKey(setting.KeyPath))
                        return false;

                    var result = ModifyBinaryByte(setting.KeyPath, setting.ValueName, setting.BinaryByteIndex.Value, byteValue);
                    logService.Log(LogLevel.Info, $"[WindowsRegistryService] Modified byte at index {setting.BinaryByteIndex.Value} to {byteValue:X2} - Success: {result}");
                    return result;
                }

                var oldValue = GetValue(setting.KeyPath, setting.ValueName);
                var valueToSet = specificValue ?? (isEnabled
                    ? setting.EnabledValue
                    : setting.DisabledValue);

                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Setting '{setting.KeyPath}\\{setting.ValueName}' - Old: {oldValue}, New: {valueToSet}");

                if (valueToSet == null)
                {
                    var result = DeleteValue(setting.KeyPath, setting.ValueName);
                    logService.Log(LogLevel.Info, $"[WindowsRegistryService] Deleted value '{setting.ValueName}' from '{setting.KeyPath}' - Success: {result}");
                    return result;
                }

                if (!CreateKey(setting.KeyPath))
                    return false;

                var setResult = SetValue(setting.KeyPath, setting.ValueName, valueToSet, setting.ValueType);

                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Set value '{setting.ValueName}' = '{valueToSet}' in '{setting.KeyPath}' - Success: {setResult}");
                return setResult;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error applying setting '{setting.KeyPath}\\{setting.ValueName}': {ex.Message}");
                return false;
            }
        }

        private static Dictionary<string, string> ParseCompositeString(string value)
        {
            var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(value))
                return pairs;

            foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIndex = entry.IndexOf('=');
                if (eqIndex > 0)
                    pairs[entry[..eqIndex]] = entry[(eqIndex + 1)..];
            }
            return pairs;
        }

        private static string BuildCompositeString(Dictionary<string, string> pairs)
        {
            if (pairs.Count == 0)
                return "";
            return string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}")) + ";";
        }

        private (RegistryKey rootKey, string subKeyPath) ParseKeyPath(string keyPath)
        {
            var parts = keyPath.Split('\\', 2);
            if (parts.Length < 2)
                throw new ArgumentException($"Invalid registry key path: {keyPath}");

            var hive = parts[0].ToUpperInvariant();

            // OTS elevation: redirect HKCU to HKU\{interactive user SID}
            if ((hive == "HKEY_CURRENT_USER" || hive == "HKCU")
                && interactiveUserService.IsOtsElevation
                && interactiveUserService.InteractiveUserSid != null)
            {
                var redirectedSubKey = $"{interactiveUserService.InteractiveUserSid}\\{parts[1]}";
                return (Registry.Users, redirectedSubKey);
            }

            var rootKey = hive switch
            {
                "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
                "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
                "HKEY_USERS" or "HKU" => Registry.Users,
                "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
                _ => throw new ArgumentException($"Invalid registry hive: {parts[0]}"),
            };

            return (rootKey, parts[1]);
        }

        public Dictionary<string, object?> GetBatchValues(IEnumerable<(string keyPath, string? valueName)> queries)
        {
            var results = new Dictionary<string, object?>();
            var queriesByHive = queries.GroupBy(q => GetHiveFromPath(q.keyPath));
            
            foreach (var hiveGroup in queriesByHive)
            {
                var rootKey = hiveGroup.Key;
                
                foreach (var (keyPath, valueName) in hiveGroup)
                {
                    try
                    {
                        var (_, subKeyPath) = ParseKeyPath(keyPath);
                        using var subKey = rootKey.OpenSubKey(subKeyPath, false);

                        var resultKey = valueName == null
                            ? $"{keyPath}\\__KEY_EXISTS__"
                            : $"{keyPath}\\{valueName}";

                        if (valueName == null)
                        {
                            results[resultKey] = subKey != null;
                        }
                        else
                        {
                            results[resultKey] = subKey?.GetValue(valueName);
                        }
                    }
                    catch
                    {
                        var resultKey = valueName == null
                            ? $"{keyPath}\\__KEY_EXISTS__"
                            : $"{keyPath}\\{valueName}";
                        results[resultKey] = null;
                    }
                }
            }
            
            return results;
        }

        private RegistryKey GetHiveFromPath(string keyPath)
        {
            var parts = keyPath.Split('\\', 2);
            var hive = parts[0].ToUpperInvariant();

            // OTS: redirect HKCU to HKU
            if ((hive == "HKEY_CURRENT_USER" || hive == "HKCU")
                && interactiveUserService.IsOtsElevation
                && interactiveUserService.InteractiveUserSid != null)
            {
                return Registry.Users;
            }

            return hive switch
            {
                "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
                "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
                "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
                "HKEY_USERS" or "HKU" => Registry.Users,
                "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
                _ => Registry.CurrentUser,
            };
        }

        private static bool CompareValues(object? current, object? desired)
        {
            return current switch
            {
                null => desired == null,
                int i when desired is int d => i == d,
                string s when desired is string ds => s.Equals(
                    ds,
                    StringComparison.OrdinalIgnoreCase
                ),
                byte[] ba when desired is byte[] dba => ba.SequenceEqual(dba),
                _ => current.Equals(desired),
            };
        }
    }
}
