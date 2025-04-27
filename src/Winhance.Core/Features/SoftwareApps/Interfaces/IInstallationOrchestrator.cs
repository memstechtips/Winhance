using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Orchestrates high-level installation and removal operations across different types of items.
    /// </summary>
    /// <remarks>
    /// This is a higher-level service that coordinates operations across different specific services.
    /// </remarks>
    public interface IInstallationOrchestrator
    {
        /// <summary>
        /// Installs an installable item based on its type.
        /// </summary>
        /// <param name="item">The item to install.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InstallAsync(
            IInstallableItem item,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an installable item based on its type.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveAsync(IInstallableItem item);

        /// <summary>
        /// Installs multiple items in batch.
        /// </summary>
        /// <param name="items">The items to install.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InstallBatchAsync(
            IEnumerable<IInstallableItem> items,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes multiple items in batch.
        /// </summary>
        /// <param name="items">The items to remove.</param>
        /// <returns>A list of results indicating success or failure for each item.</returns>
        Task<List<(string Name, bool Success, string? Error)>> RemoveBatchAsync(
            IEnumerable<IInstallableItem> items);
    }
}