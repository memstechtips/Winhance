using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

[SupportedOSPlatform("windows")]
public class WindowsRegistryService(ILogService logService, IInteractiveUserService interactiveUserService) : IWindowsRegistryService
{
    private static object? GetWriteValue(object?[]? values) => values?.FirstOrDefault(v => v != null);

    /// <summary>
    /// Gets the value to write when a parent cascades a disable to this setting.
    /// If DisabledValue has a second element, use it (even if null, which means delete).
    /// Otherwise, fall back to the normal first-non-null disabled value.
    /// This allows settings to declare e.g. DisabledValue = [1, null] where:
    ///   - Index 0 (1): written when the user explicitly disables the setting
    ///   - Index 1 (null): written when the parent cascades a disable (deletes the value)
    /// </summary>
    private static object? GetParentDisableValue(object?[]? disabledValues) =>
        disabledValues?.Length > 1 ? disabledValues[1] : GetWriteValue(disabledValues);

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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to create key '{keyPath}': {ex.Message}");
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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to set value '{keyPath}\\{valueName}': {ex.Message}");
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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to get value '{keyPath}\\{valueName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Minimum number of path segments (backslash-separated) required in the
    /// sub-key portion of a registry path before <see cref="DeleteSubKeyTree"/>
    /// is allowed. This prevents accidental deletion of top-level hive branches
    /// like <c>HKLM\SOFTWARE</c>.
    /// </summary>
    private const int MinDeleteDepth = 2;

    /// <summary>
    /// Top-level registry branches that must never be deleted via
    /// <see cref="DeleteKey"/>. Comparison is case-insensitive.
    /// </summary>
    internal static readonly HashSet<string> ProtectedSubKeyRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        @"SOFTWARE\Microsoft\Windows",
        @"SOFTWARE\Microsoft\Windows NT",
        @"SOFTWARE\Policies",
        @"SYSTEM\CurrentControlSet",
        @"SYSTEM\CurrentControlSet\Services",
    };

    public bool DeleteKey(string keyPath)
    {
        try
        {
            if (!KeyExists(keyPath))
                return true;

            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);

            // Safeguard: reject paths that are too shallow (e.g. "SOFTWARE" or "SOFTWARE\Microsoft")
            var segments = subKeyPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < MinDeleteDepth)
            {
                logService.Log(LogLevel.Warning,
                    $"[WindowsRegistryService] Refusing to delete shallow registry key '{keyPath}' (depth {segments.Length} < {MinDeleteDepth})");
                return false;
            }

            // Safeguard: reject paths that start with a protected root
            foreach (var protectedRoot in ProtectedSubKeyRoots)
            {
                if (subKeyPath.Equals(protectedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    logService.Log(LogLevel.Warning,
                        $"[WindowsRegistryService] Refusing to delete protected registry key '{keyPath}'");
                    return false;
                }
            }

            rootKey.DeleteSubKeyTree(subKeyPath, false);
            return true;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to delete key '{keyPath}': {ex.Message}");
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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to delete value '{keyPath}\\{valueName}': {ex.Message}");
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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to check key existence '{keyPath}': {ex.Message}");
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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to check value existence '{keyPath}\\{valueName}': {ex.Message}");
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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to get subkey names for '{keyPath}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte newValue)
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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to get binary byte at index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
            return null;
        }
    }

    public bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit)
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
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to check bit in '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Locks a registry key to read-only for SYSTEM and TrustedInstaller,
    /// preventing Windows from resetting the value.
    /// Administrators retain full control to allow Winhance to unlock later.
    /// </summary>
    public bool LockRegistryKey(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(
                subKeyPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ChangePermissions | RegistryRights.ReadKey | RegistryRights.TakeOwnership);

            if (key == null)
            {
                logService.Log(LogLevel.Warning, $"[WindowsRegistryService] Cannot lock key '{keyPath}': key not found");
                return false;
            }

            var security = key.GetAccessControl();

            // Ensure Administrators own the key
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(adminsSid);

            // Disable inheritance and convert existing rules to explicit
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // Remove all existing access rules
            foreach (RegistryAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                security.RemoveAccessRule(rule);
            }

            // Grant Administrators full control (so Winhance can unlock later)
            security.AddAccessRule(new RegistryAccessRule(
                adminsSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            // Grant SYSTEM read-only (prevents Windows from writing)
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            security.AddAccessRule(new RegistryAccessRule(
                systemSid,
                RegistryRights.ReadKey,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            key.SetAccessControl(security);

            logService.Log(LogLevel.Info, $"[WindowsRegistryService] Locked registry key '{keyPath}' to read-only for SYSTEM");
            return true;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WindowsRegistryService] Failed to lock registry key '{keyPath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores default permissions on a registry key, re-enabling
    /// inheritance and granting SYSTEM full control again.
    /// </summary>
    public bool UnlockRegistryKey(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(
                subKeyPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ChangePermissions | RegistryRights.ReadKey | RegistryRights.TakeOwnership);

            if (key == null)
            {
                logService.Log(LogLevel.Warning, $"[WindowsRegistryService] Cannot unlock key '{keyPath}': key not found");
                return false;
            }

            var security = key.GetAccessControl();

            // Ensure Administrators own the key
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(adminsSid);

            // Remove all explicit rules
            foreach (RegistryAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            {
                security.RemoveAccessRule(rule);
            }

            // Re-enable inheritance
            security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);

            // Grant SYSTEM full control
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            security.AddAccessRule(new RegistryAccessRule(
                systemSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            // Grant Administrators full control
            security.AddAccessRule(new RegistryAccessRule(
                adminsSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            key.SetAccessControl(security);

            logService.Log(LogLevel.Info, $"[WindowsRegistryService] Unlocked registry key '{keyPath}' - restored SYSTEM full control");
            return true;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WindowsRegistryService] Failed to unlock registry key '{keyPath}': {ex.Message}");
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

    /// <summary>
    /// Read-merge-write of one sub-key inside a packed ";"-delimited "key=value" REG_SZ value: ensures the key
    /// exists, re-reads the current composite value, sets (or, when <paramref name="subValue"/> is null, removes)
    /// the given sub-key, then writes the merged string back (trailing ";", OrdinalIgnoreCase sub-keys via
    /// <see cref="ParseCompositeString"/>). Extracted verbatim from the old ApplySetting CompositeStringKey branch
    /// so the old apply and the new IStateWriter share identical behaviour; the caller resolves the sub-value.
    /// </summary>
    public bool SetCompositeSubValue(string keyPath, string valueName, string compositeKey, string? subValue)
    {
        try
        {
            if (!CreateKey(keyPath))
                return false;

            var currentComposite = ValueExists(keyPath, valueName)
                ? (GetValue(keyPath, valueName)?.ToString() ?? "")
                : "";

            var pairs = ParseCompositeString(currentComposite);

            if (subValue != null)
                pairs[compositeKey] = subValue;
            else
                pairs.Remove(compositeKey);

            var mergedValue = BuildCompositeString(pairs);
            var compositeResult = SetValue(keyPath, valueName, mergedValue, RegistryValueKind.String);

            logService.Log(LogLevel.Info,
                $"[WindowsRegistryService] Updated composite key '{compositeKey}' to '{subValue}' in '{keyPath}\\{valueName}' - Full value: '{mergedValue}' - Success: {compositeResult}");
            return compositeResult;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error setting composite key '{compositeKey}' in '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
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
                catch (Exception ex)
                {
                    logService.LogDebug($"[WindowsRegistryService] Failed to get batch value for '{keyPath}\\{valueName}': {ex.Message}");
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
            _ => throw new ArgumentException($"Unrecognized registry hive: '{hive}' in path '{keyPath}'"),
        };
    }

    private static bool CompareValues(object? current, object? desired)
    {
        return current switch
        {
            null => desired == null,
            bool b when desired is int d => (b ? 1 : 0) == d,
            byte b when desired is int d => b == d,
            byte b when desired is byte d => b == d,
            int i when desired is int d => i == d,
            int i when desired is long d => i == d,
            int i when desired is byte d => i == d,
            long l when desired is long d => l == d,
            long l when desired is int d => l == d,
            string s when desired is string ds => s.Equals(
                ds,
                StringComparison.OrdinalIgnoreCase
            ),
            byte[] ba when desired is byte[] dba => ba.SequenceEqual(dba),
            _ => current.Equals(desired),
        };
    }
}
