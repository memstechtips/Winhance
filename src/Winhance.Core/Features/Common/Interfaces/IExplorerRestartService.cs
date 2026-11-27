using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Every path that restarts the shell goes through here. Two hard-won rules: (1) restarts are SINGLE-FLIGHT -
// overlapping kill/wait cycles race each other, and repeated shell deaths in a short window make winlogon's
// AutoRestartShell stop relaunching it, which can leave the user with no shell at all; (2) the manual relaunch
// goes through the INTERACTIVE USER TOKEN, never Process.Start from our own process - Winhance is always elevated
// (requireAdministrator), so a plain relaunch would start the shell at high integrity.
public interface IExplorerRestartService
{
    // Clears the pending-restart state on success only; a failed restart leaves it intact so the user keeps a way to retry.
    Task<OperationResult> RestartAsync();

    // Payload-free and asynchronous, so it costs nothing per window.
    void BroadcastShellRefresh();

    // The expensive half: one of its three messages carries a string payload and must be sent synchronously at a
    // per-top-level-window timeout - so it is sent only for settings that can change the theme, and after a shell relaunch.
    void BroadcastThemeRefresh();
}
