using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for installing Windows capabilities.
    /// </summary>
    public interface ICapabilityInstallationService : IInstallationService<CapabilityInfo>
    {
        /// <summary>
        /// Installs a capability.
        /// </summary>
        /// <param name="capability">The capability to install.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        Task<OperationResult<bool>> InstallCapabilityAsync(CapabilityInfo capability, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a capability can be installed.
        /// </summary>
        /// <param name="capability">The capability to check.</param>
        /// <returns>An operation result indicating if the capability can be installed, with error details if not.</returns>
        Task<OperationResult<bool>> CanInstallCapabilityAsync(CapabilityInfo capability);
    }
}