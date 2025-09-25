using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Unified service interface for app discovery, loading, and status management.
    /// </summary>
    public interface IAppService
    {
        #region App Discovery & Loading

        /// <summary>
        /// Gets all installable applications.
        /// </summary>
        /// <returns>A collection of installable applications.</returns>
        Task<IEnumerable<ItemDefinition>> GetInstallableAppsAsync();

        /// <summary>
        /// Gets all standard (built-in) applications.
        /// </summary>
        /// <returns>A collection of standard applications.</returns>
        Task<IEnumerable<ItemDefinition>> GetStandardAppsAsync();

        /// <summary>
        /// Gets all available Windows capabilities.
        /// </summary>
        /// <returns>A collection of Windows capabilities.</returns>
        Task<IEnumerable<ItemDefinition>> GetCapabilitiesAsync();

        /// <summary>
        /// Gets all available Windows optional features.
        /// </summary>
        /// <returns>A collection of Windows optional features.</returns>
        Task<IEnumerable<ItemDefinition>> GetOptionalFeaturesAsync();

        #endregion

        #region Status Management

        /// <summary>
        /// Checks if an application is installed.
        /// </summary>
        /// <param name="packageName">The package name to check.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the application is installed; otherwise, false.</returns>
        Task<bool> IsAppInstalledAsync(string packageName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if Microsoft Edge is installed.
        /// </summary>
        /// <returns>True if Edge is installed; otherwise, false.</returns>
        Task<bool> IsEdgeInstalledAsync();

        /// <summary>
        /// Checks if OneDrive is installed.
        /// </summary>
        /// <returns>True if OneDrive is installed; otherwise, false.</returns>
        Task<bool> IsOneDriveInstalledAsync();

        Task<bool> GetItemInstallStatusAsync(ItemDefinition item);

        /// <summary>
        /// Gets the installation status of multiple items by definition.
        /// </summary>
        /// <param name="definitions">The item definitions to check.</param>
        /// <returns>A dictionary mapping package keys to installation status.</returns>
        Task<Dictionary<string, bool>> GetBatchInstallStatusAsync(IEnumerable<ItemDefinition> definitions);

        /// <summary>
        /// Gets detailed installation status of an app.
        /// </summary>
        /// <param name="appId">The app ID to check.</param>
        /// <returns>The detailed installation status.</returns>
        Task<InstallStatus> GetInstallStatusAsync(string appId);

        Task RefreshInstallationStatusAsync(IEnumerable<ItemDefinition> items);

        /// <summary>
        /// Sets the installation status of an app.
        /// </summary>
        /// <param name="appId">The app ID to update.</param>
        /// <param name="status">The new installation status.</param>
        /// <returns>True if the status was updated successfully; otherwise, false.</returns>
        Task<bool> SetInstallStatusAsync(string appId, InstallStatus status);

        /// <summary>
        /// Clears the status cache.
        /// </summary>
        void ClearStatusCache();

        #endregion
    }
}