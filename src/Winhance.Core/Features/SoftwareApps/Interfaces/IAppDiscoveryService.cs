using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for discovering and retrieving information about installed applications.
    /// </summary>
    public interface IAppDiscoveryService
    {
        /// <summary>
        /// Gets all standard (built-in) applications.
        /// </summary>
        /// <returns>A collection of standard applications.</returns>
        Task<IEnumerable<AppInfo>> GetStandardAppsAsync();

        /// <summary>
        /// Gets all installable third-party applications.
        /// </summary>
        /// <returns>A collection of installable applications.</returns>
        Task<IEnumerable<AppInfo>> GetInstallableAppsAsync();

        /// <summary>
        /// Gets all available Windows capabilities.
        /// </summary>
        /// <returns>A collection of Windows capabilities.</returns>
        Task<IEnumerable<CapabilityInfo>> GetCapabilitiesAsync();

        /// <summary>
        /// Gets all available Windows optional features.
        /// </summary>
        /// <returns>A collection of Windows optional features.</returns>
        Task<IEnumerable<FeatureInfo>> GetOptionalFeaturesAsync();

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

        /// <summary>
        /// Checks if OneNote is installed.
        /// </summary>
        /// <returns>True if OneNote is installed; otherwise, false.</returns>
        Task<bool> IsOneNoteInstalledAsync();
    }
}