using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Generic interface for installation services.
    /// </summary>
    /// <typeparam name="T">The type of item to install, which must implement IInstallableItem.</typeparam>
    public interface IInstallationService<T> where T : IInstallableItem
    {
        /// <summary>
        /// Installs an item.
        /// </summary>
        /// <param name="item">The item to install.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        Task<OperationResult<bool>> InstallAsync(
            T item,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an item can be installed.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>An operation result indicating if the item can be installed, with error details if not.</returns>
        Task<OperationResult<bool>> CanInstallAsync(T item);
    }
}