using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for installing Windows optional features.
    /// </summary>
    public interface IFeatureInstallationService : IInstallationService<FeatureInfo>
    {
        /// <summary>
        /// Installs a feature.
        /// </summary>
        /// <param name="feature">The feature to install.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        Task<OperationResult<bool>> InstallFeatureAsync(FeatureInfo feature, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a feature can be installed.
        /// </summary>
        /// <param name="feature">The feature to check.</param>
        /// <returns>An operation result indicating if the feature can be installed, with error details if not.</returns>
        Task<OperationResult<bool>> CanInstallFeatureAsync(FeatureInfo feature);
    }
}