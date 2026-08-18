using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <inheritdoc cref="IExplorerRestartService"/>
public sealed class ExplorerRestartService : IExplorerRestartService
{
    private const string ExplorerProcessName = "explorer";
    private const string ExplorerExecutable = "explorer.exe";

    // winlogon's AutoRestartShell only covers the shell stopping UNEXPECTEDLY, so it is worth waiting for on
    // the terminate fallback and nowhere else - a short 500ms x 4 = 2s courtesy window. Then allow longer for
    // the manual launch to produce a visible shell (500ms x 20 = 10s).
    private const int AutoRestartAttempts = 4;
    private const int ManualRestartAttempts = 20;
    private const int PollDelayMs = 500;

    // How long to wait for the killed Explorer processes to actually exit.
    private const int KillTimeoutMs = 5000;

    // How long the polite "please exit" request gets before we stop being polite. Explorer normally goes in
    // well under a second; this is only the point at which we stop believing it will.
    private const int GracefulExitTimeoutMs = 3000;

    private readonly IWindowsUIManagementService _uiManagement;
    private readonly IInteractiveUserService _interactiveUser;
    private readonly IPendingRestartService _pendingRestart;
    private readonly ILogService _logService;
    private readonly Func<int, Task> _delay;

    // Serializes restarts. This is the fix for the concurrent-kill race, so it must wrap the WHOLE
    // sequence (kill + wait + relaunch + verify), not just the kill.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ExplorerRestartService(
        IWindowsUIManagementService uiManagement,
        IInteractiveUserService interactiveUser,
        IPendingRestartService pendingRestart,
        ILogService logService)
        : this(uiManagement, interactiveUser, pendingRestart, logService, delay: null)
    {
    }

    /// <summary>Test seam: lets the suite exercise the poll loops without real delays.</summary>
    internal ExplorerRestartService(
        IWindowsUIManagementService uiManagement,
        IInteractiveUserService interactiveUser,
        IPendingRestartService pendingRestart,
        ILogService logService,
        Func<int, Task>? delay)
    {
        _uiManagement = uiManagement ?? throw new ArgumentNullException(nameof(uiManagement));
        _interactiveUser = interactiveUser ?? throw new ArgumentNullException(nameof(interactiveUser));
        _pendingRestart = pendingRestart ?? throw new ArgumentNullException(nameof(pendingRestart));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _delay = delay ?? (ms => Task.Delay(ms));
    }

    /// <inheritdoc />
    public void BroadcastShellRefresh() => _uiManagement.BroadcastShellRefresh();

    /// <inheritdoc />
    public void BroadcastThemeRefresh() => _uiManagement.BroadcastThemeRefresh();

    /// <inheritdoc />
    public async Task<OperationResult> RestartAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Task.Run because the gate is normally UNCONTENDED: WaitAsync then completes synchronously and
            // everything after it runs INLINE on the caller's thread. For the pending-restart bar that thread
            // is the UI thread, so the window froze for the whole restart and the IsRestarting progress ring
            // never rendered. Nothing in the core has thread affinity - the pending-restart service is
            // explicitly thread-safe and its listener marshals to the dispatcher itself.
            return await Task.Run(() => RestartCoreAsync()).ConfigureAwait(false);
        }
        finally
        {
            // Still runs on every path: a faulted or cancelled core surfaces through the await above, so the
            // release cannot be skipped.
            _gate.Release();
        }
    }

    private async Task<OperationResult> RestartCoreAsync()
    {
        // Captured BEFORE anything is killed. The token is harvested from the live explorer.exe, so
        // once the shell is dead there is nothing left to harvest - and that is precisely when it is
        // needed. Not gated on OTS: Winhance is always elevated, and an elevated Process.Start cannot
        // bring the shell back.
        IShellRelaunchToken? relaunchToken = null;

        try
        {
            // GENERIC ONLY, deliberately. The shell is about to go down; a relaunched Explorer reads the
            // theme straight from the registry when it starts, and the post-relaunch broadcast below
            // re-notifies everything anyway. Sending the theme set here as well would pay the
            // per-top-level-window SendMessageTimeout cost twice for one restart and change nothing.
            _uiManagement.BroadcastShellRefresh();

            // True ONLY on the branch where the shell was TERMINATED rather than asked to leave. That is the
            // one branch winlogon might react to, and the one the log needs to name.
            bool terminated = false;

            if (_uiManagement.IsProcessRunning(ExplorerProcessName))
            {
                relaunchToken = _interactiveUser.CaptureShellRelaunchToken();

                _logService.Log(LogLevel.Info, "[ExplorerRestartService] Stopping Explorer");

                // ASK FIRST. A graceful exit lets Explorer save the desktop icon layout and folder view
                // preferences; terminating it throws them away. The message behind this is UNDOCUMENTED, so
                // it is strictly best-effort - which is why the fallback below is the OLD behaviour, unchanged.
                if (_uiManagement.TryGracefulShellExit(GracefulExitTimeoutMs))
                {
                    _logService.Log(LogLevel.Info, "[ExplorerRestartService] Explorer exited gracefully");
                }
                else
                {
                    terminated = true;
                    _logService.Log(LogLevel.Info,
                        "[ExplorerRestartService] Explorer did not exit gracefully in time; terminating it");

                    // KillProcessAndWait, never KillProcess: Process.Kill only requests termination, so
                    // polling straight afterwards sees the dying process and reads it as "already back".
                    if (!_uiManagement.KillProcessAndWait(ExplorerProcessName, KillTimeoutMs))
                        _logService.Log(LogLevel.Warning,
                            "[ExplorerRestartService] Explorer did not confirm exit within the timeout; continuing");
                }
            }
            else
            {
                _logService.Log(LogLevel.Info, "[ExplorerRestartService] Explorer was not running; starting it");
            }

            // Give winlogon's AutoRestartShell its chance first - when it fires, the shell comes back at the
            // correct integrity level with no help from us. It covers an UNEXPECTED termination only, so it is
            // worth waiting for on the terminate branch and nowhere else: after a graceful exit (and when
            // Explorer was not running to begin with) that wait was six seconds spent waiting for something
            // that by design was never going to happen.
            bool backOnItsOwn = terminated
                && await WaitForExplorerAsync(AutoRestartAttempts).ConfigureAwait(false);

            if (!backOnItsOwn)
            {
                // IDEMPOTENCY, and it is load-bearing. It is NOT established whether recent Windows 11 builds
                // re-spawn the shell by themselves after the graceful-exit message, so look for a live shell
                // WINDOW before spawning one - a second Explorer over a working shell opens a stray folder
                // window on the user's desktop. The probe fails closed, so an unreadable answer still
                // relaunches: a spare window is an annoyance, no shell is a disaster.
                if (_uiManagement.IsShellWindowAlive())
                {
                    _logService.Log(LogLevel.Info,
                        "[ExplorerRestartService] A shell is already back; not launching a second Explorer");
                }
                else
                {
                    _logService.Log(LogLevel.Info, "[ExplorerRestartService] Relaunching Explorer");

                    // Fire-and-forget: neither relaunch path can report failure (the fallback is a
                    // void API), so the poll below is the only real verdict on whether the shell is back.
                    TryRelaunch(relaunchToken);

                    if (!await WaitForExplorerAsync(ManualRestartAttempts).ConfigureAwait(false))
                        return Fail("Explorer did not come back after a manual launch");
                }
            }

            // A FRESH shell gets the FULL picture, theme included. This is the one broadcast genuinely worth
            // its cost: Explorer has just come back and everything that re-registered with it needs
            // re-notifying. It is also already off the UI thread (RestartAsync wraps this whole core in
            // Task.Run), so the synchronous ImmersiveColorSet send cannot freeze the window.
            _uiManagement.BroadcastThemeRefresh();
            _uiManagement.BroadcastShellRefresh();

            // Cleared on SUCCESS ONLY. A pending state that was not actually satisfied has to survive so
            // the bar stays up and the user keeps a way to retry.
            _pendingRestart.Clear();
            _logService.Log(LogLevel.Info, "[ExplorerRestartService] Explorer restarted");
            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logService.LogError("Failed to restart Explorer", ex);
            return OperationResult.Failed("Failed to restart Explorer", ex);
        }
        finally
        {
            relaunchToken?.Dispose();
        }
    }

    /// <summary>
    /// Relaunches the shell with the token captured before the kill. Falls back to
    /// LaunchProcessAsInteractiveUser - a void API that is gated on OTS elevation and degrades to
    /// Process.Start otherwise, so it is a last resort, not the plan.
    /// <para>
    /// Returns nothing on purpose. The fallback cannot report failure, so there is no honest bool to
    /// hand back; WaitForExplorerAsync is the verdict. The two warnings below are kept distinct
    /// because they are different faults with different fixes: no token could be captured at all,
    /// versus a token that was captured and then failed to launch (CreateProcessWithTokenW).
    /// </para>
    /// </summary>
    private void TryRelaunch(IShellRelaunchToken? relaunchToken)
    {
        if (relaunchToken is null)
        {
            _logService.Log(LogLevel.Warning,
                "[ExplorerRestartService] No shell token was captured; falling back to the interactive-user launch");
        }
        else if (relaunchToken.TryLaunch(ExplorerExecutable))
        {
            return;
        }
        else
        {
            _logService.Log(LogLevel.Warning,
                "[ExplorerRestartService] The captured shell token failed to launch Explorer; falling back to the interactive-user launch");
        }

        _interactiveUser.LaunchProcessAsInteractiveUser(ExplorerExecutable);
    }

    private OperationResult Fail(string message)
    {
        _logService.Log(LogLevel.Error, $"[ExplorerRestartService] {message}");
        return OperationResult.Failed(message);
    }

    /// <summary>
    /// Polls for Explorer coming back. Delays FIRST: this is only ever called just after the process
    /// was killed, so checking before waiting is what produced the false "it is already back".
    /// </summary>
    private async Task<bool> WaitForExplorerAsync(int attempts)
    {
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            await _delay(PollDelayMs).ConfigureAwait(false);

            if (_uiManagement.IsProcessRunning(ExplorerProcessName))
                return true;
        }

        return false;
    }
}
