using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for removing Windows applications.
    /// </summary>
    public interface IAppRemovalService
    {
        /// <summary>
        /// Removes an application.
        /// </summary>
        /// <param name="app">The application to remove.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        Task<OperationResult<bool>> RemoveAppAsync(AppInfo app, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a removal script for an application.
        /// </summary>
        /// <param name="app">The application to generate a removal script for.</param>
        /// <returns>An operation result containing the removal script or error details.</returns>
        Task<OperationResult<string>> GenerateRemovalScriptAsync(AppInfo app);
    }
}