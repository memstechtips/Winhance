using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

[SupportedOSPlatform("windows")]
public class WindowsRegistryService(ILogService logService, IInteractiveUserService interactiveUserService) : IWindowsRegistryService
{

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

    public RegistryValueKind? GetValueKind(string keyPath, string valueName)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            if (key is null)
                return null;
            // GetValueKind throws when the value does not exist, so check first rather than catching.
            return key.GetValue(valueName) is null ? null : key.GetValueKind(valueName);
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to get value kind '{keyPath}\\{valueName}': {ex.Message}");
            return null;
        }
    }

    // Prevents accidental deletion of top-level hive branches like HKLM\SOFTWARE.
    private const int MinDeleteDepth = 2;

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

            var segments = subKeyPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < MinDeleteDepth)
            {
                logService.Log(LogLevel.Warning,
                    $"[WindowsRegistryService] Refusing to delete shallow registry key '{keyPath}' (depth {segments.Length} < {MinDeleteDepth})");
                return false;
            }

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

    // Null when the caller must REFUSE the write rather than destroy the value.
    private byte[]? ResolveBinaryEditBuffer(string keyPath, string valueName, object? currentValue, int byteIndex)
    {
        var buffer = BinaryValueRecovery.Resolve(currentValue, byteIndex);

        if (buffer is null)
        {
            logService.Log(
                LogLevel.Error,
                $"[WindowsRegistryService] Refusing to edit '{keyPath}\\{valueName}': expected REG_BINARY but "
                    + $"found {currentValue?.GetType().Name ?? "null"}, which has no safe byte representation. "
                    + "The value was left untouched.");
            return null;
        }

        if (BinaryValueRecovery.IsRecoveredFromString(currentValue))
        {
            logService.Log(
                LogLevel.Warning,
                $"[WindowsRegistryService] '{keyPath}\\{valueName}' is stored as a string but the catalog "
                    + "expects REG_BINARY. Recovered its bytes from the UTF-16 content and rewriting the "
                    + "value as REG_BINARY.");
        }

        return buffer;
    }

    public bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte newValue)
    {
        try
        {
            var currentValue = GetValue(keyPath, valueName);
            var currentBytes = ResolveBinaryEditBuffer(keyPath, valueName, currentValue, byteIndex);
            if (currentBytes is null)
                return false;

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

    public bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit)
    {
        try
        {
            var currentValue = GetValue(keyPath, valueName);
            var currentBytes = ResolveBinaryEditBuffer(keyPath, valueName, currentValue, byteIndex);
            if (currentBytes is null)
                return false;

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

    // SYSTEM and TrustedInstaller lose write so Windows cannot reset the value; Administrators keep full control so
    // Winhance can unlock later.
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

            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(adminsSid);

            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            foreach (RegistryAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                security.RemoveAccessRule(rule);
            }

            security.AddAccessRule(new RegistryAccessRule(
                adminsSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

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

            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(adminsSid);

            foreach (RegistryAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            {
                security.RemoveAccessRule(rule);
            }

            security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);

            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            security.AddAccessRule(new RegistryAccessRule(
                systemSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

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
}
