using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Customize.Interfaces
{
    /// <summary>
    /// Service interface for managing Taskbar customization settings.
    /// Handles taskbar appearance, behavior, and cleanup operations.
    /// </summary>
    public interface ITaskbarService : IDomainService
    {
        /// <summary>
        /// Executes taskbar cleanup operation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteTaskbarCleanupAsync();
    }
}
