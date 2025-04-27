using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for a service that manages scheduled tasks for script execution.
    /// </summary>
    public interface IScheduledTaskService
    {
        /// <summary>
        /// Registers a scheduled task to run the specified script.
        /// </summary>
        /// <param name="script">The script to register as a scheduled task.</param>
        /// <returns>True if the task was registered successfully, false otherwise.</returns>
        Task<bool> RegisterScheduledTaskAsync(RemovalScript script);

        /// <summary>
        /// Unregisters a scheduled task with the specified name.
        /// </summary>
        /// <param name="taskName">The name of the task to unregister.</param>
        /// <returns>True if the task was unregistered successfully, false otherwise.</returns>
        Task<bool> UnregisterScheduledTaskAsync(string taskName);

        /// <summary>
        /// Checks if a scheduled task with the specified name is registered.
        /// </summary>
        /// <param name="taskName">The name of the task to check.</param>
        /// <returns>True if the task exists, false otherwise.</returns>
        Task<bool> IsTaskRegisteredAsync(string taskName);
    }
}