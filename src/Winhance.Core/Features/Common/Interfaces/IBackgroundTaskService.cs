using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Keeping removed apps removed, in either mode, plus the one sign-in task and the locked ProgramData folder.
public interface IBackgroundTaskService
{
    // Raised when a check finishes, so the Settings page can show the new state.
    event EventHandler? Maintained;

    KeepRemovedMode GetMode();

    // Converts between the modes: Winhance -> Scripts writes scripts and tasks from the kept list, Scripts -> Winhance
    // imports the scripts into the kept list and deletes them.
    Task<OperationResult> SetModeAsync(KeepRemovedMode mode);

    Task<bool> IsKeepRemovedOnAsync();

    // Winhance mode keeps the list and stops checking; Scripts mode writes or deletes the scripts and tasks.
    Task<OperationResult> SetKeepRemovedOnAsync(bool on);

    // Needs admin to change anything; both are false/no-op for a standard user.

    // Folder locked and, in Scripts mode, every removal task points into it.
    Task<bool> IsTaskSafetyOkAsync();
    Task RepairTaskSafetyAsync();

    IReadOnlyList<KeptApp> GetKeptApps();
    KeepRemovedStatus GetStatus();

    // Adds the catalog ids to the kept list and turns keeping on. Unknown ids are skipped.
    Task<OperationResult> AddKeptAppsAsync(IEnumerable<string> appIds);
    void RemoveKeptApp(string appId);

    // Scripts mode: every removal script / task pair that exists, with what the task and the disk really say.
    Task<IReadOnlyList<RemovalTaskState>> GetRemovalTasksAsync();
    Task<OperationResult> RepairRemovalTaskAsync(string name);
    Task<OperationResult> RemoveRemovalTaskAsync(string name);

    // Winhance mode: secure the folder, find kept apps that came back, remove them, write Status. Does nothing in
    // Scripts mode or while keeping is off.
    Task<KeepRemovedStatus> MaintainAsync(CancellationToken ct = default);

    Task<SignInOptions> GetSignInOptionsAsync();
    Task<OperationResult> SetSignInOptionAsync(string flag, bool on);

    // Runs on every start: locks the folder, deletes the old SYSTEM task, settles the mode, folds the old sign-in tasks into one.
    Task InitializeAsync();

    // The Edge link redirect Winhance's Edge removal leaves behind (its task and folder), or null when neither exists.
    // Name is the task, ScriptPath the folder.
    Task<RemovalTaskState?> GetEdgeRedirectAsync();

    // Every task in Winhance's task folder except the sign-in task, the removal tasks and the Edge link redirect.
    Task<IReadOnlyList<WinhanceTaskInfo>> GetOtherTasksAsync();
}
