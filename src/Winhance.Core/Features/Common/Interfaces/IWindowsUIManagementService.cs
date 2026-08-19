namespace Winhance.Core.Features.Common.Interfaces;

public interface IWindowsUIManagementService
{
    bool IsProcessRunning(string processName);
    void KillProcess(string processName);

    /// <summary>
    /// Kills every instance of <paramref name="processName"/> and waits for each to actually exit,
    /// returning true when they are all gone.
    ///
    /// <see cref="KillProcess"/> is NOT enough on its own: Process.Kill only REQUESTS termination and
    /// returns immediately, so a poll for "is it running" straight afterwards still sees the dying
    /// process. For a shell restart that reads as "Explorer is already back", the restart reports
    /// success, and the fallback relaunch never runs - leaving the user with no shell.
    /// </summary>
    bool KillProcessAndWait(string processName, int timeoutMs);

    /// <summary>
    /// Asks Explorer to shut the shell down GRACEFULLY - the undocumented taskbar message behind the
    /// hidden Ctrl+Shift+right-click "Exit Explorer" item - and waits up to <paramref name="timeoutMs"/>
    /// for the explorer.exe processes that were alive when the request went out to actually exit.
    ///
    /// Graceful matters: Explorer flushes and saves its state on the way out (desktop icon layout, folder
    /// view preferences). <see cref="KillProcessAndWait"/> throws all of that away.
    ///
    /// Returns true ONLY when every one of those processes exited, so the caller may relaunch. False means
    /// NOT PROVEN, and the caller MUST fall back to <see cref="KillProcessAndWait"/> - the message is
    /// undocumented, so this is best-effort by construction and the kill is the guaranteed path.
    ///
    /// The verdict is deliberately "the processes we asked to exit are gone", not "no explorer.exe is
    /// running": Windows may re-spawn the shell inside the wait window, and reading that as a failure would
    /// make the caller terminate the shell it just got back.
    /// </summary>
    bool TryGracefulShellExit(int timeoutMs);

    /// <summary>
    /// True when a live shell (taskbar) window exists right now. A WINDOW probe, not a process probe: a
    /// leftover explorer.exe hosting a folder window is not a shell, and a shell Windows re-spawned by
    /// itself is one.
    ///
    /// This is what keeps a relaunch idempotent. It fails CLOSED - any error reports false - so an
    /// unreadable probe makes the caller launch a shell rather than risk leaving the user without one.
    /// </summary>
    bool IsShellWindowAlive();

    /// <summary>
    /// Broadcasts the GENERIC shell-refresh message - one WM_SETTINGCHANGE with a NULL lParam - so running
    /// apps re-read what they cache, without anything being restarted. No payload means SendNotifyMessage
    /// can be used, which returns immediately and costs NOTHING per window. This is what EVERY
    /// Explorer-restart setting gets.
    ///
    /// The theme/colour half of what used to be one combined broadcast now lives in
    /// <see cref="BroadcastThemeRefresh"/>. It is the expensive half, and most settings cannot benefit
    /// from it.
    ///
    /// Killing and relaunching Explorer is <see cref="IExplorerRestartService"/>'s job, deliberately -
    /// it is never a side effect of applying a setting. See that type for why.
    /// </summary>
    void BroadcastShellRefresh();

    /// <summary>
    /// Broadcasts the THEME/colour notifications: WM_SYSCOLORCHANGE, WM_THEMECHANGE and the
    /// "ImmersiveColorSet" WM_SETTINGCHANGE.
    ///
    /// EXPENSIVE, which is the whole reason it is separate. The ImmersiveColorSet message carries a string
    /// payload, so it MUST be sent synchronously (the buffer is freed the moment the call returns) and
    /// SendMessageTimeout charges its timeout PER TOP-LEVEL WINDOW on a broadcast - measured at ~2s per
    /// toggle right after an Explorer restart, while tray apps are re-registering.
    ///
    /// Send it only where it can change something: a setting that DECLARES it changes how Windows looks
    /// (<c>ApplyBehavior.NotifyWindows</c> carrying <c>WindowsChange.Appearance</c>), and after a shell
    /// relaunch.
    /// </summary>
    void BroadcastThemeRefresh();

    void BroadcastRegionalSettingChange();
}
