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
}
