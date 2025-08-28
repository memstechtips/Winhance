using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Infrastructure.Features.Common.WindowsRegistry
{
    [SupportedOSPlatform("windows")]
    public class WindowsRegistryService : IWindowsRegistryService
    {
        private readonly ILogService _logService;

        public WindowsRegistryService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public bool CreateKey(string keyPath)
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

        public bool IsSettingApplied(RegistrySetting setting)
        {
            try
            {
                if (setting == null)
                    return false;

                if (string.IsNullOrEmpty(setting.ValueName))
                {
                    return KeyExists(setting.KeyPath);
                }

                if (!KeyExists(setting.KeyPath))
                {
                    return setting.AbsenceMeansEnabled;
                }

                if (!ValueExists(setting.KeyPath, setting.ValueName))
                {
                    return setting.AbsenceMeansEnabled;
                }

                var currentValue = GetValue(setting.KeyPath, setting.ValueName);
                if (currentValue == null)
                    return false;

                var valueToCompare = setting.EnabledValue ?? setting.RecommendedValue;
                if (CompareValues(currentValue, valueToCompare))
                    return true;

                if (
                    setting.DisabledValue != null
                    && CompareValues(currentValue, setting.DisabledValue)
                )
                    return false;

                return false; // Modified state now maps to false
            }
            catch (Exception)
            {
                return false; // Error state now maps to false
            }
        }

        public bool ApplySetting(RegistrySetting setting, bool isEnabled)
        {
            if (setting == null)
                return false;

            try
            {
                if (string.IsNullOrEmpty(setting.ValueName))
                {
                    return isEnabled ? CreateKey(setting.KeyPath) : DeleteKey(setting.KeyPath);
                }

                var oldValue = GetValue(setting.KeyPath, setting.ValueName);
                var valueToSet = isEnabled
                    ? (setting.EnabledValue ?? setting.RecommendedValue)
                    : setting.DisabledValue;

                if (valueToSet == null)
                {
                    return DeleteValue(setting.KeyPath, setting.ValueName);
                }

                if (!CreateKey(setting.KeyPath))
                    return false;

                return SetValue(
                    setting.KeyPath,
                    setting.ValueName,
                    valueToSet,
                    setting.ValueType
                );
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static (RegistryKey rootKey, string subKeyPath) ParseKeyPath(string keyPath)
        {
            var parts = keyPath.Split('\\', 2);
            if (parts.Length < 2)
                throw new ArgumentException($"Invalid registry key path: {keyPath}");

            var rootKey = parts[0].ToUpperInvariant() switch
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
