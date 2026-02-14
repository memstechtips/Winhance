using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Launches regedit.exe and navigates to a specific registry path.
/// </summary>
public static class RegeditLauncher
{
    /// <summary>
    /// Checks whether the given registry key path exists.
    /// Accepts paths like "HKEY_LOCAL_MACHINE\SOFTWARE\..." or "HKLM\SOFTWARE\...".
    /// </summary>
    public static bool KeyExists(string registryPath)
    {
        try
        {
            var (root, subKey) = ParsePath(registryPath);
            if (root == null || subKey == null) return false;
            using var key = root.OpenSubKey(subKey);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    private static (RegistryKey? root, string? subKey) ParsePath(string path)
    {
        var separatorIndex = path.IndexOf('\\');
        if (separatorIndex < 0) return (null, null);

        var hive = path[..separatorIndex].ToUpperInvariant();
        var subKey = path[(separatorIndex + 1)..];

        RegistryKey? root = hive switch
        {
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => null
        };

        return (root, subKey);
    }

    public static void OpenAtPath(string registryPath)
    {
        try
        {
            // Normalize path with "Computer\" prefix if not present
            var fullPath = registryPath.StartsWith("Computer\\", StringComparison.OrdinalIgnoreCase)
                ? registryPath
                : $"Computer\\{registryPath}";

            // Write LastKey to HKCU so regedit opens at the correct location
            using var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
            key?.SetValue("LastKey", fullPath);

            Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });
        }
        catch
        {
            // Best-effort — silently ignore failures (e.g., regedit not found)
        }
    }
}
