using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class WindowsUIManagementService : IWindowsUIManagementService
{
    // The shell's taskbar window class. Its presence IS the definition of "a shell is running", and it is
    // the window the graceful-exit message is posted to.
    private const string ShellTrayWindowClass = "Shell_TrayWnd";
    private const string ShellProcessName = "explorer";

    // UNDOCUMENTED. Posted to the taskbar window it asks Explorer to shut the shell down
    // GRACEFULLY - the path behind the hidden Ctrl+Shift+right-click "Exit Explorer" item; Explorer saves its state
    // on the way out, which Process.Kill does not. Reference: ExplorerPatcher's utility.h ExitExplorer(); the same
    // value appears in long-standing AutoHotkey samples. Best-effort by construction: callers keep the kill as fallback.
    private const uint WM_SHELL_GRACEFUL_EXIT = 0x5B4;

    private readonly ILogService _logService;

    public WindowsUIManagementService(ILogService logService)
    {
        _logService = logService;
    }

    public bool IsProcessRunning(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            var isRunning = processes.Length > 0;
            foreach (var process in processes)
            {
                process.Dispose();
            }
            return isRunning;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error checking if process {processName} is running", ex);
            return false;
        }
    }

    public void KillProcess(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to kill process {processName}", ex);
        }
    }

    public bool KillProcessAndWait(string processName, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        try
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    // Process.Kill only REQUESTS termination. Without this wait the caller's very next
                    // "is it running" poll still sees the dying process.
                    var remaining = (int)Math.Max(0, deadline - Environment.TickCount64);
                    if (!process.WaitForExit(remaining))
                        _logService.Log(LogLevel.Warning,
                            $"Process {processName} did not exit within the timeout");
                }
                catch (Exception ex)
                {
                    // Already exited between enumeration and Kill is the common, harmless case.
                    _logService.Log(LogLevel.Debug, $"Killing {processName}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            return !IsProcessRunning(processName);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to kill process {processName}", ex);
            return false;
        }
    }

    public bool IsShellWindowAlive()
    {
        try
        {
            return !PInvoke.FindWindow(ShellTrayWindowClass, null).IsNull;
        }
        catch (Exception ex)
        {
            // Fail CLOSED. "No shell" makes the caller launch one; claiming a shell is alive off a probe
            // that threw would leave the user with no taskbar.
            _logService.Log(LogLevel.Debug, $"Shell window probe failed: {ex.Message}");
            return false;
        }
    }

    public bool TryGracefulShellExit(int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        try
        {
            var taskbar = PInvoke.FindWindow(ShellTrayWindowClass, null);
            if (taskbar.IsNull)
            {
                _logService.Log(LogLevel.Debug,
                    "No taskbar window found, so there is nothing to post the graceful shell-exit message to");
                return false;
            }

            // Handles FIRST. Once the shell has been asked to leave there may be nothing left to enumerate,
            // and waiting on the handles we already hold is the only precise verdict available.
            var processes = Process.GetProcessesByName(ShellProcessName);
            if (processes.Length == 0)
                return false;

            try
            {
                if (!PInvoke.PostMessage(taskbar, WM_SHELL_GRACEFUL_EXIT, default, default))
                {
                    _logService.Log(LogLevel.Debug, "Posting the graceful shell-exit message failed");
                    return false;
                }

                bool allExited = true;
                foreach (var process in processes)
                {
                    try
                    {
                        var remaining = (int)Math.Max(0, deadline - Environment.TickCount64);
                        if (!process.WaitForExit(remaining))
                            allExited = false;
                    }
                    catch (Exception ex)
                    {
                        // Cannot PROVE it exited, so report failure and let the caller terminate it.
                        _logService.Log(LogLevel.Debug,
                            $"Waiting for {ShellProcessName} to exit gracefully: {ex.Message}");
                        allExited = false;
                    }
                }

                return allExited;
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logService.LogError(
                "Graceful Explorer exit failed; the caller falls back to terminating it", ex);
            return false;
        }
    }

    public void BroadcastShellRefresh()
    {
        try
        {
            // The generic WM_SETTINGCHANGE, NULL lParam. NO POINTER PAYLOAD, so there is nothing a receiver
            // can read after we return: SendNotifyMessage returns immediately and is Microsoft's own
            // recommendation for HWND_BROADCAST. It costs nothing per window, which is exactly why this is
            // the one message every Explorer-restart setting gets.
            PInvoke.SendNotifyMessage(HWND.HWND_BROADCAST, PInvoke.WM_SETTINGCHANGE, default, default);
        }
        catch (Exception ex)
        {
            _logService.LogError("Error broadcasting shell refresh", ex);
        }
    }

    public unsafe void BroadcastThemeRefresh()
    {
        try
        {
            // These two carry NO POINTER PAYLOAD, so there is nothing a receiver can read after we return:
            // SendNotifyMessage returns immediately and is Microsoft's own recommendation for HWND_BROADCAST.
            PInvoke.SendNotifyMessage(HWND.HWND_BROADCAST, PInvoke.WM_SYSCOLORCHANGE, default, default);
            PInvoke.SendNotifyMessage(HWND.HWND_BROADCAST, PInvoke.WM_THEMECHANGED, default, default);

            string themeChanged = "ImmersiveColorSet";
            IntPtr themeChangedPtr = Marshal.StringToHGlobalUni(themeChanged);

            try
            {
                // MUST STAY SYNCHRONOUS. lParam is a pointer to the string above and the finally below frees
                // it, so an asynchronous send would return before the receivers had read it and they would read
                // freed memory.
                //
                // THIS CALL IS THE COST. SendMessageTimeout charges its timeout PER TOP-LEVEL WINDOW on a
                // broadcast (Microsoft docs: "if you specify a five second time-out period and there are three
                // top-level windows that fail to process the message, you could have up to a 15 second delay"),
                // so 100ms x ~20 busy windows is the 2s stall a user saw with nothing in the log. Splitting the
                // broadcast is what keeps the other ~43 Explorer-restart settings from paying it.
                //
                // unsafe only because lpdwResult is a raw pointer on the generated import; it is optional
                // and no receiver's result is wanted, so it is left off.
                PInvoke.SendMessageTimeout(
                    HWND.HWND_BROADCAST, PInvoke.WM_SETTINGCHANGE,
                    default, themeChangedPtr, SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG, 100);
            }
            finally
            {
                Marshal.FreeHGlobal(themeChangedPtr);
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Error broadcasting theme refresh", ex);
        }
    }

    public unsafe void BroadcastRegionalSettingChange()
    {
        IntPtr intlPtr = Marshal.StringToHGlobalUni("intl");
        try
        {
            PInvoke.SendMessageTimeout(
                HWND.HWND_BROADCAST, PInvoke.WM_SETTINGCHANGE,
                default, intlPtr, SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG, 1000);
        }
        finally
        {
            Marshal.FreeHGlobal(intlPtr);
        }
    }
}
