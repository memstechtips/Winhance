using Microsoft.Win32;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Utilities;

public class RegeditLauncher(
    IInteractiveUserService interactiveUserService,
    IProcessExecutor processExecutor,
    ILogService logService) : IRegeditLauncher
{
    public void OpenAtPath(string registryPath)
    {
        try
        {
            var navigatePath = registryPath;

            if (navigatePath.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
                navigatePath = $"HKEY_CURRENT_USER\\{navigatePath[5..]}";
            else if (navigatePath.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
                navigatePath = $"HKEY_LOCAL_MACHINE\\{navigatePath[5..]}";

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
