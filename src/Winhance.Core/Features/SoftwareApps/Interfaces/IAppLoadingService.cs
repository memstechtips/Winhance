using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for loading application information and status.
    /// </summary>
    public interface IAppLoadingService
    {
        /// <summary>
        /// Loads applications and their installation status.
        /// </summary>
        /// <returns>An operation result containing a collection of applications with their installation status or error details.</returns>
        Task<OperationResult<IEnumerable<AppInfo>>> LoadAppsAsync();

        /// <summary>
        /// Refreshes the installation status of applications.
        /// </summary>
        /// <param name="apps">The applications to refresh.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        Task<OperationResult<bool>> RefreshInstallationStatusAsync(IEnumerable<AppInfo> apps);

        /// <summary>
        /// Gets the installation status for an app by ID.
        /// </summary>
        /// <param name="appId">The app ID.</param>
        /// <returns>An operation result containing the installation status or error details.</returns>
        Task<OperationResult<InstallStatus>> GetInstallStatusAsync(string appId);

        /// <summary>
        /// Sets the installation status for an app.
        /// </summary>
        /// <param name="appId">The app ID.</param>
        /// <param name="status">The new status.</param>
        /// <returns>An operation result indicating success or failure with error details.</returns>
        Task<OperationResult<bool>> SetInstallStatusAsync(string appId, InstallStatus status);

        /// <summary>
        /// Loads Windows capabilities.
        /// </summary>
        /// <returns>A collection of capability information.</returns>
        Task<IEnumerable<CapabilityInfo>> LoadCapabilitiesAsync();

        /// <summary>
        /// Gets the installation status for an installable item.
        /// </summary>
        /// <param name="item">The installable item.</param>
        /// <returns>True if the item is installed, false otherwise.</returns>
        Task<bool> GetItemInstallStatusAsync(IInstallableItem item);

        /// <summary>
        /// Gets the installation status for multiple package IDs.
        /// </summary>
        /// <param name="packageIds">The package IDs to check.</param>
        /// <returns>A dictionary mapping package IDs to their installation status.</returns>
        Task<Dictionary<string, bool>> GetBatchInstallStatusAsync(IEnumerable<string> packageIds);

        /// <summary>
        /// Clears the status cache.
        /// </summary>
        void ClearStatusCache();
    }
}