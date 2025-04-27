using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Provides package management functionality across the application.
    /// </summary>
    public interface IPackageManager
    {
        /// <summary>
        /// Gets the logging service.
        /// </summary>
        ILogService LogService { get; }

        /// <summary>
        /// Gets the app discovery service.
        /// </summary>
        IAppService AppDiscoveryService { get; }

        /// <summary>
        /// Gets the app removal service.
        /// </summary>
        IInstallationService<AppInfo> AppRemovalService { get; }

        /// <summary>
        /// Gets the special app handler service.
        /// </summary>
        ISpecialAppHandlerService SpecialAppHandlerService { get; }

        /// <summary>
        /// Gets the script generation service.
        /// </summary>
        IScriptGenerationService ScriptGenerationService { get; }

        /// <summary>
        /// Gets all installable applications.
        /// </summary>
        /// <returns>A collection of installable applications.</returns>
        Task<IEnumerable<AppInfo>> GetInstallableAppsAsync();

        /// <summary>
        /// Gets all standard (built-in) applications.
        /// </summary>
        /// <returns>A collection of standard applications.</returns>
        Task<IEnumerable<AppInfo>> GetStandardAppsAsync();

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
        /// Removes a specific application.
        /// </summary>
        /// <param name="packageName">The package name to remove.</param>
        /// <param name="isCapability">Whether the package is a capability.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        Task<bool> RemoveAppAsync(string packageName, bool isCapability = false);

        /// <summary>
        /// Checks if an application is installed.
        /// </summary>
        /// <param name="packageName">The package name to check.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the application is installed; otherwise, false.</returns>
        Task<bool> IsAppInstalledAsync(string packageName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes Microsoft Edge.
        /// </summary>
        /// <returns>True if Edge was removed successfully; otherwise, false.</returns>
        Task<bool> RemoveEdgeAsync();

        /// <summary>
        /// Removes OneDrive.
        /// </summary>
        /// <returns>True if OneDrive was removed successfully; otherwise, false.</returns>
        Task<bool> RemoveOneDriveAsync();

        /// <summary>
        /// Removes a special application.
        /// </summary>
        /// <param name="appHandlerType">The type of special app handler to use.</param>
        /// <returns>True if the application was removed successfully; otherwise, false.</returns>
        Task<bool> RemoveSpecialAppAsync(string appHandlerType);

        /// <summary>
        /// Removes multiple applications in a batch operation.
        /// </summary>
        /// <param name="apps">A list of app information to remove.</param>
        /// <returns>A list of results indicating success or failure for each application.</returns>
        Task<List<(string Name, bool Success, string? Error)>> RemoveAppsInBatchAsync(
            List<(string PackageName, bool IsCapability, string? SpecialHandlerType)> apps);

        /// <summary>
        /// Registers a removal task.
        /// </summary>
        /// <param name="script">The removal script to register.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RegisterRemovalTaskAsync(RemovalScript script);
    }
}