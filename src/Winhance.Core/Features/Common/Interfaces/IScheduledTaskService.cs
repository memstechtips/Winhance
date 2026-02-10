using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IScheduledTaskService
    {
        Task<bool> RegisterScheduledTaskAsync(RemovalScript script);
        Task<bool> UnregisterScheduledTaskAsync(string taskName);
        Task<bool> IsTaskRegisteredAsync(string taskName);
        Task<bool> RunScheduledTaskAsync(string taskName);
        Task<bool> CreateUserLogonTaskAsync(string taskName, string command, string username, bool deleteAfterRun = true);
        Task<bool> EnableTaskAsync(string taskPath);
        Task<bool> DisableTaskAsync(string taskPath);
        Task<bool> IsTaskEnabledAsync(string taskPath);
        Task<bool> EnableTasksByFolderAsync(string folderPath);
        Task<bool> DisableTasksByFolderAsync(string folderPath);
    }
}