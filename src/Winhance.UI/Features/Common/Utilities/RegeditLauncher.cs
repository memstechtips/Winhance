using System;
using System.Diagnostics;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Launches regedit.exe and navigates to a specific registry path.
/// </summary>
public class RegeditLauncher(
    IInteractiveUserService interactiveUserService,
    IProcessExecutor processExecutor,
    ILogService logService) : IRegeditLauncher
{
    /// <summary>
    /// Checks whether the given registry key path exists.
    /// Accepts paths like "HKEY_LOCAL_MACHINE\SOFTWARE\..." or "HKLM\SOFTWARE\...".
    /// In OTS mode, HKCU paths are redirected to HKU\{interactive user SID}.
    /// </summary>
    public bool KeyExists(string registryPath)
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

    private (RegistryKey? root, string? subKey) ParsePath(string path)
    {
        var separatorIndex = path.IndexOf('\\');
        if (separatorIndex < 0) return (null, null);

        var hive = path[..separatorIndex].ToUpperInvariant();
        var subKey = path[(separatorIndex + 1)..];

        // OTS: redirect HKCU to HKU\{interactive user SID}
        if ((hive == "HKEY_CURRENT_USER" || hive == "HKCU")
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

    public void OpenAtPath(string registryPath)
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

            bool isOts = interactiveUserService.IsOtsElevation
                && interactiveUserService.InteractiveUserSid != null
                && interactiveUserService.HasInteractiveUserToken;

            if (isOts)
            {
                // OTS: write LastKey to the interactive user's hive (HKU\{SID})
                var sid = interactiveUserService.InteractiveUserSid!;
                using var key = Registry.Users.CreateSubKey(
                    $@"{sid}\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
                key?.SetValue("LastKey", fullPath);

                // Launch regedit as the interactive user
                interactiveUserService.LaunchProcessAsInteractiveUser("regedit.exe");
            }
            else
            {
                // Normal mode: write LastKey to admin's HKCU and launch normally
                using var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
                key?.SetValue("LastKey", fullPath);

                processExecutor.ShellExecuteAsync("regedit.exe").FireAndForget(logService);
            }
        }
        catch
        {
            // Best-effort — silently ignore failures (e.g., regedit not found)
        }
    }
}
