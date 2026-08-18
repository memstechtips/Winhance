namespace Winhance.Core.Features.Common.Interfaces;

public interface IWindowsUIManagementService
{
    bool IsProcessRunning(string processName);
    void KillProcess(string processName);

    // KillProcess alone is NOT enough: Process.Kill only REQUESTS termination and returns immediately, so a poll
    // straight afterwards still sees the dying process. For a shell restart that reads as "Explorer is already
    // back", the restart reports success, and the fallback relaunch never runs - leaving the user with no shell.
    bool KillProcessAndWait(string processName, int timeoutMs);

    // The undocumented taskbar message behind the hidden Ctrl+Shift+right-click "Exit Explorer" item. Graceful
    // matters: Explorer saves its state on the way out (icon layout, folder views); a kill throws that away.
    // True ONLY when every explorer.exe alive at the request exited; false means NOT PROVEN and the caller MUST fall
    // back to KillProcessAndWait. The verdict is deliberately "the processes we asked to exit are gone", not
    // "no explorer.exe is running": Windows may re-spawn the shell inside the wait window.
    bool TryGracefulShellExit(int timeoutMs);

    // A WINDOW probe, not a process probe: a leftover explorer.exe hosting a folder window is not a shell, and a
    // shell Windows re-spawned by itself is one - this keeps a relaunch idempotent. Fails CLOSED (any error reports
    // false) so an unreadable probe launches a shell rather than risk leaving the user without one.
    bool IsShellWindowAlive();

    // One WM_SETTINGCHANGE with a NULL lParam: no payload, so SendNotifyMessage returns immediately and costs nothing
    // per window. Every Explorer-restart setting gets this; the theme/colour half is BroadcastThemeRefresh.
    // Restarting Explorer is IExplorerRestartService's job, never a side effect of applying a setting.
    void BroadcastShellRefresh();

    // WM_SYSCOLORCHANGE, WM_THEMECHANGE and the "ImmersiveColorSet" WM_SETTINGCHANGE. EXPENSIVE: ImmersiveColorSet
    // carries a string payload, so it MUST be sent synchronously (the buffer is freed when the call returns) and
    // SendMessageTimeout charges its timeout PER TOP-LEVEL WINDOW - measured at ~2s per toggle right after an
    // Explorer restart. Send it only for a setting that DECLARES it changes how Windows looks
    // (WindowsChange.Appearance) and after a shell relaunch.
    void BroadcastThemeRefresh();

    void BroadcastRegionalSettingChange();
}
