using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for installing Windows applications.
    /// </summary>
    public interface IAppInstallationService : IInstallationService<AppInfo>
    {
        /// <summary>
        /// Installs an application.
        /// </summary>
        /// <param name="app">The application to install.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        Task<OperationResult<bool>> InstallAppAsync(AppInfo app, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an application can be installed.
        /// </summary>
        /// <param name="app">The application to check.</param>
        /// <returns>An operation result indicating if the application can be installed, with error details if not.</returns>
        Task<OperationResult<bool>> CanInstallAppAsync(AppInfo app);
    }
}