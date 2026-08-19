using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// The single owner of restarting Explorer. Every path that needs the shell restarted goes through here
/// - the setting-apply path, the config-import path and the pending-restart bar.
///
/// Two hard-won rules are baked into the implementation:
///
/// (1) Restarts are SINGLE-FLIGHT. Overlapping kill/wait cycles race each other - one cycle kills the
///     Explorer another is waiting for - and repeated shell deaths in a short window make winlogon's
///     AutoRestartShell stop relaunching it. Together that could leave a user with no shell at all,
///     recoverable only by starting explorer.exe from Task Manager.
///
/// (2) The manual relaunch goes through the INTERACTIVE USER TOKEN, never Process.Start from our own
///     process. Winhance is always elevated (app.manifest declares requireAdministrator), so a plain
///     relaunch tries to start the shell at high integrity and the safety net never actually fires.
/// </summary>
public interface IExplorerRestartService
{
    /// <summary>
    /// Restarts Explorer and, on success only, clears the pending-restart state. Serialized: concurrent
    /// callers queue rather than overlap. A failed restart deliberately leaves the pending state intact
    /// so the user keeps a way to retry.
    /// </summary>
    Task<OperationResult> RestartAsync();

    /// <summary>
    /// Broadcasts the generic shell-refresh message without restarting anything, so a change that CAN take
    /// effect live does so immediately. Payload-free and asynchronous, so it costs nothing per window.
    /// </summary>
    void BroadcastShellRefresh();

    /// <summary>
    /// Broadcasts the theme/colour notifications. The expensive half - one of its three messages carries a
    /// string payload and must be sent synchronously, at a per-top-level-window timeout - so it is sent only
    /// for settings that can actually change the theme, and after a shell relaunch.
    /// </summary>
    void BroadcastThemeRefresh();
}
