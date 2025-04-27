using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Adapter class that implements IAppService by delegating to AppDiscoveryService
    /// and other services for the additional functionality required by IAppService.
    /// </summary>
    public class AppServiceAdapter : IAppService
    {
        private readonly AppDiscoveryService _appDiscoveryService;
        private readonly ILogService _logService;
        private Dictionary<string, InstallStatus> _installStatusCache = new Dictionary<string, InstallStatus>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AppServiceAdapter"/> class.
        /// </summary>
        /// <param name="appDiscoveryService">The app discovery service to delegate to.</param>
        /// <param name="logService">The logging service.</param>
        public AppServiceAdapter(AppDiscoveryService appDiscoveryService, ILogService logService)
        {
            _appDiscoveryService = appDiscoveryService ?? throw new ArgumentNullException(nameof(appDiscoveryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<AppInfo>> GetInstallableAppsAsync()
        {
            return await _appDiscoveryService.GetInstallableAppsAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<AppInfo>> GetStandardAppsAsync()
        {
            return await _appDiscoveryService.GetStandardAppsAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<CapabilityInfo>> GetCapabilitiesAsync()
        {
            return await _appDiscoveryService.GetCapabilitiesAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<FeatureInfo>> GetOptionalFeaturesAsync()
        {
            return await _appDiscoveryService.GetOptionalFeaturesAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> IsAppInstalledAsync(string packageName, CancellationToken cancellationToken = default)
        {
            // Implement using the concrete AppDiscoveryService
            return await _appDiscoveryService.IsAppInstalledAsync(packageName, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<bool> IsEdgeInstalledAsync()
        {
            return await _appDiscoveryService.IsEdgeInstalledAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> IsOneDriveInstalledAsync()
        {
            return await _appDiscoveryService.IsOneDriveInstalledAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> GetItemInstallStatusAsync(IInstallableItem item)
        {
            if (item == null)
                return false;

            return await IsAppInstalledAsync(item.PackageId);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, bool>> GetBatchInstallStatusAsync(IEnumerable<string> packageIds)
        {
            var result = new Dictionary<string, bool>();
            
            // Check for special apps first
            foreach (var packageId in packageIds)
            {
                // Special handling for Edge and OneDrive
                if (packageId.Equals("Microsoft Edge", StringComparison.OrdinalIgnoreCase) ||
                    packageId.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
                    packageId.Equals("Edge", StringComparison.OrdinalIgnoreCase))
                {
                    result[packageId] = await _appDiscoveryService.IsEdgeInstalledAsync();
                }
                else if (packageId.Equals("OneDrive", StringComparison.OrdinalIgnoreCase))
                {
                    result[packageId] = await _appDiscoveryService.IsOneDriveInstalledAsync();
                }
            }
            
            // Get the remaining package IDs that aren't special apps
            var remainingPackageIds = packageIds.Where(id =>
                !result.ContainsKey(id) &&
                !id.Equals("Microsoft Edge", StringComparison.OrdinalIgnoreCase) &&
                !id.Equals("msedge", StringComparison.OrdinalIgnoreCase) &&
                !id.Equals("Edge", StringComparison.OrdinalIgnoreCase) &&
                !id.Equals("OneDrive", StringComparison.OrdinalIgnoreCase));
            
            // Use the concrete AppDiscoveryService's batch method for the remaining packages
            var batchResults = await _appDiscoveryService.GetInstallationStatusBatchAsync(remainingPackageIds);
            
            // Merge the results
            foreach (var pair in batchResults)
            {
                result[pair.Key] = pair.Value;
            }
            
            return result;
        }

        /// <inheritdoc/>
        public async Task<InstallStatus> GetInstallStatusAsync(string appId)
        {
            if (string.IsNullOrEmpty(appId))
                return InstallStatus.Failed;

            // Check cache first
            if (_installStatusCache.TryGetValue(appId, out var cachedStatus))
            {
                return cachedStatus;
            }

            // Check if the app is installed
            bool isInstalled = await IsAppInstalledAsync(appId);
            var status = isInstalled ? InstallStatus.Success : InstallStatus.NotFound;

            // Cache the result
            _installStatusCache[appId] = status;

            return status;
        }

        /// <inheritdoc/>
        public async Task RefreshInstallationStatusAsync(IEnumerable<IInstallableItem> items)
        {
            // Clear the cache for the specified items
            foreach (var item in items)
            {
                if (_installStatusCache.ContainsKey(item.PackageId))
                {
                    _installStatusCache.Remove(item.PackageId);
                }
            }

            // Refresh the status for each item
            foreach (var item in items)
            {
                await GetInstallStatusAsync(item.PackageId);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetInstallStatusAsync(string appId, InstallStatus status)
        {
            if (string.IsNullOrEmpty(appId))
                return false;

            // Update the cache
            _installStatusCache[appId] = status;
            return true;
        }

        /// <inheritdoc/>
        public void ClearStatusCache()
        {
            _installStatusCache.Clear();

            // Clear the app discovery service's cache
            _appDiscoveryService.ClearInstallationStatusCache();
        }
    }
}