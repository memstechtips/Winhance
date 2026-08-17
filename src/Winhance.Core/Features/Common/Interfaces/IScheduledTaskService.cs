using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>Registers, removes and runs WINHANCE's own scheduled tasks, which live in its own task-scheduler
/// folder. To read or toggle a task Windows already owns, use <see cref="IScheduledTaskStateService"/>.</summary>
public interface IScheduledTaskService
{
    Task<OperationResult> RegisterScheduledTaskAsync(RemovalScript script);
    Task<OperationResult> UnregisterScheduledTaskAsync(string taskName);
    Task<bool> IsTaskRegisteredAsync(string taskName);
    Task<OperationResult> RunScheduledTaskAsync(string taskName);
}
