using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IScheduledTaskService
    {
        Task<OperationResult> RegisterScheduledTaskAsync(RemovalScript script);
        Task<OperationResult> UnregisterScheduledTaskAsync(string taskName);
        Task<bool> IsTaskRegisteredAsync(string taskName);
        Task<OperationResult> RunScheduledTaskAsync(string taskName);
        Task<OperationResult> CreateUserLogonTaskAsync(string taskName, string command, string username, bool deleteAfterRun = true);
        Task<OperationResult> EnableTaskAsync(string taskPath);
        Task<OperationResult> DisableTaskAsync(string taskPath);
        Task<bool?> IsTaskEnabledAsync(string taskPath);
    }
}