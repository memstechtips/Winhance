namespace Winhance.Core.Features.Common.Interfaces;

// Winhance deliberately no longer restarts Explorer as a side effect of applying a setting: once per toggle meant
// several shell kills within seconds, which could leave the user with no shell at all. The restart is deferred to
// an explicit user action (IExplorerRestartService). Stores ids only; localizing them is the UI's job.
public interface IPendingRestartService
{
    void Register(string settingId);

    bool IsPending { get; }

    // A snapshot, never a live view onto internal state.
    IReadOnlyCollection<string> PendingSettingIds { get; }

    // Called after a SUCCESSFUL Explorer restart, never before.
    void Clear();
}
