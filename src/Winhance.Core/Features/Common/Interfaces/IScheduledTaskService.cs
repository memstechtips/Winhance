using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// WINHANCE's own tasks, in its own task-scheduler folder; tasks Windows owns are IScheduledTaskStateService.
public interface IScheduledTaskService
{
    Task<OperationResult> RegisterScheduledTaskAsync(RemovalScript script);
    Task<OperationResult> UnregisterScheduledTaskAsync(string taskName);
    Task<bool> IsTaskRegisteredAsync(string taskName);
    Task<OperationResult> RunScheduledTaskAsync(string taskName);

    // Runs command at the current user's sign-in, elevated, so an app that requires admin starts without a UAC prompt.
    Task<OperationResult> RegisterSignInTaskAsync(string taskName, string command, string arguments);

    // A task name is one per PC, so a sign-in task registered by another account is not this user's. A null SID is the
    // account Winhance runs as.
    Task<bool> IsSignInTaskRegisteredForAsync(string taskName, string? userSid);
}
