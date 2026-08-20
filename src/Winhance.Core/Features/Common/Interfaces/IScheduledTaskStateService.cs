using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Tasks that WINDOWS owns, by full path (the catalog's task targets); IScheduledTaskService is for Winhance's
// own tasks - the two share only a COM connection. Synchronous because the Task Scheduler COM API blocks; a
// caller that must not block wraps these in its own Task.Run.
public interface IScheduledTaskStateService
{
    OperationResult SetTaskEnabled(string taskPath, bool enabled);

    // One connection for many tasks - connecting is the expensive part, and detection re-reads every path on each
    // navigation. Null = not registered on this machine.
    IReadOnlyDictionary<string, bool?> GetTasksEnabled(IReadOnlyCollection<string> taskPaths);
}
