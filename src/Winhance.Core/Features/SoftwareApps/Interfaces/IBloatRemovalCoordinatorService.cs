using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for coordinating the removal of Windows apps, capabilities, and features using the BloatRemoval script.
    /// </summary>
    public interface IBloatRemovalCoordinatorService
    {
        /// <summary>
        /// Adds Windows apps to the BloatRemoval script.
        /// </summary>
        /// <param name="apps">The list of apps to add to the script.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the result of the operation.</returns>
        Task<OperationResult<bool>> AddAppsToScriptAsync(
            List<AppInfo> apps,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds Windows capabilities to the BloatRemoval script.
        /// </summary>
        /// <param name="capabilities">The list of capabilities to add to the script.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the result of the operation.</returns>
        Task<OperationResult<bool>> AddCapabilitiesToScriptAsync(
            List<CapabilityInfo> capabilities,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds Windows optional features to the BloatRemoval script.
        /// </summary>
        /// <param name="features">The list of features to add to the script.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the result of the operation.</returns>
        Task<OperationResult<bool>> AddFeaturesToScriptAsync(
            List<FeatureInfo> features,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the BloatRemoval script to remove all items added to it.
        /// </summary>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the result of the execution.</returns>
        Task<OperationResult<bool>> ExecuteScriptAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes Windows apps, capabilities, and features in a single operation by adding them to the script and executing it.
        /// </summary>
        /// <param name="apps">The list of apps to remove.</param>
        /// <param name="capabilities">The list of capabilities to remove.</param>
        /// <param name="features">The list of features to remove.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the result of the removal.</returns>
        Task<OperationResult<bool>> RemoveItemsAsync(
            List<AppInfo>? apps = null,
            List<CapabilityInfo>? capabilities = null,
            List<FeatureInfo>? features = null,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
