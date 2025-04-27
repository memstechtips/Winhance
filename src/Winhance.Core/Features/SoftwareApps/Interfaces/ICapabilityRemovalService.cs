using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for removing Windows capabilities.
    /// </summary>
    public interface ICapabilityRemovalService
    {
        /// <summary>
        /// Removes a capability.
        /// </summary>
        /// <param name="capability">The capability to remove.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True if the removal was successful; otherwise, false.</returns>
        Task<bool> RemoveCapabilityAsync(CapabilityInfo capability, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a capability can be removed.
        /// </summary>
        /// <param name="capability">The capability to check.</param>
        /// <returns>True if the capability can be removed; otherwise, false.</returns>
        Task<bool> CanRemoveCapabilityAsync(CapabilityInfo capability);
    }
}