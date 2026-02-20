using System;
using System.Diagnostics;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Launches regedit.exe and navigates to a specific registry path.
/// </summary>
public static class RegeditLauncher
{
    /// <summary>
    /// Checks whether the given registry key path exists.
    /// Accepts paths like "HKEY_LOCAL_MACHINE\SOFTWARE\..." or "HKLM\SOFTWARE\...".
    /// In OTS mode, HKCU paths are redirected to HKU\{interactive user SID}.
    /// </summary>
    public static bool KeyExists(string registryPath, IInteractiveUserService? interactiveUserService = null)
    {
        try
        {
            var (root, subKey) = ParsePath(registryPath, interactiveUserService);
            if (root == null || subKey == null) return false;
            using var key = root.OpenSubKey(subKey);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    private static (RegistryKey? root, string? subKey) ParsePath(string path, IInteractiveUserService? interactiveUserService = null)
    {
        var separatorIndex = path.IndexOf('\\');
        if (separatorIndex < 0) return (null, null);

        var hive = path[..separatorIndex].ToUpperInvariant();
        var subKey = path[(separatorIndex + 1)..];

        // OTS: redirect HKCU to HKU\{interactive user SID}
        if ((hive == "HKEY_CURRENT_USER" || hive == "HKCU")
            && interactiveUserService != null
            && interactiveUserService.IsOtsElevation
            && interactiveUserService.InteractiveUserSid != null)
        {
            return (Registry.Users, $@"{interactiveUserService.InteractiveUserSid}\{subKey}");
        }

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

    public static void OpenAtPath(string registryPath, IInteractiveUserService? interactiveUserService = null)
    {
        try
        {
            var navigatePath = registryPath;

            // Normalize short hive names to long names for regedit
            if (navigatePath.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
                navigatePath = $"HKEY_CURRENT_USER\\{navigatePath[5..]}";
            else if (navigatePath.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
                navigatePath = $"HKEY_LOCAL_MACHINE\\{navigatePath[5..]}";

            // Normalize path with "Computer\" prefix if not present
            var fullPath = navigatePath.StartsWith("Computer\\", StringComparison.OrdinalIgnoreCase)
                ? navigatePath
                : $"Computer\\{navigatePath}";

            bool isOts = interactiveUserService != null
                && interactiveUserService.IsOtsElevation
                && interactiveUserService.InteractiveUserSid != null
                && interactiveUserService.HasInteractiveUserToken;

            if (isOts)
            {
                // OTS: write LastKey to the interactive user's hive (HKU\{SID})
                // so regedit launched as that user opens at the right location.
                // The path stays as HKEY_CURRENT_USER\... because regedit will
                // run as the standard user where HKCU is their own hive.
                var sid = interactiveUserService!.InteractiveUserSid!;
                using var key = Registry.Users.CreateSubKey(
                    $@"{sid}\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
                key?.SetValue("LastKey", fullPath);

                // Launch regedit as the interactive user so HKCU shows the
                // standard user's hive, not the admin's.
                interactiveUserService.LaunchProcessAsInteractiveUser("regedit.exe");
            }
            else
            {
                // Normal mode: write LastKey to admin's HKCU and launch normally
                using var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
                key?.SetValue("LastKey", fullPath);

                Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });
            }
        }
        catch
        {
            // Best-effort — silently ignore failures (e.g., regedit not found)
        }
    }
}
