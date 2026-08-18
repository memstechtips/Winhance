namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Tracks settings that have been applied but whose change only takes visible effect once Explorer is
/// restarted.
///
/// Winhance deliberately no longer restarts Explorer as a side effect of applying a setting. Doing so
/// once per toggle meant a user changing several Explorer tweaks in a row triggered several shell kills
/// within seconds, which could leave them with no shell at all. The restart is deferred to an explicit
/// user action instead - see <see cref="IExplorerRestartService"/>.
///
/// Stores setting IDs only. Localizing them into display names is the UI layer's job.
/// </summary>
public interface IPendingRestartService
{
    /// <summary>Records that <paramref name="settingId"/> needs an Explorer restart. Idempotent per ID.</summary>
    void Register(string settingId);

    /// <summary>True while at least one setting is waiting on a restart.</summary>
    bool IsPending { get; }

    /// <summary>A snapshot of the waiting setting IDs. Never a live view onto internal state.</summary>
    IReadOnlyCollection<string> PendingSettingIds { get; }

    /// <summary>Drops all pending state. Called after a SUCCESSFUL Explorer restart, never before.</summary>
    void Clear();
}
