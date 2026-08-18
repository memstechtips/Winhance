using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>Reads and toggles scheduled tasks that WINDOWS owns, addressed by full path - the settings
/// catalog's task targets. Separate from <see cref="IScheduledTaskService"/>, which registers and runs
/// Winhance's own tasks in its own folder; the two share only a COM connection.
///
/// Synchronous because the Task Scheduler COM API blocks; a caller that must not block wraps these in its
/// own Task.Run.</summary>
public interface IScheduledTaskStateService
{
    OperationResult SetTaskEnabled(string taskPath, bool enabled);

    /// <summary>Enabled state of many tasks over a SINGLE connection - connecting is the expensive part and
    /// detection re-reads every task path on each navigation. Every requested path gets an entry; null means
    /// the task is not registered on this machine.</summary>
    IReadOnlyDictionary<string, bool?> GetTasksEnabled(IReadOnlyCollection<string> taskPaths);
}
