using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for removing Windows optional features.
    /// </summary>
    public interface IFeatureRemovalService
    {
        /// <summary>
        /// Removes a feature.
        /// </summary>
        /// <param name="feature">The feature to remove.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True if the removal was successful; otherwise, false.</returns>
        Task<bool> RemoveFeatureAsync(FeatureInfo feature, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a feature can be removed.
        /// </summary>
        /// <param name="feature">The feature to check.</param>
        /// <returns>True if the feature can be removed; otherwise, false.</returns>
        Task<bool> CanRemoveFeatureAsync(FeatureInfo feature);
    }
}