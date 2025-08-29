using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    public class AppLoadingService : IAppLoadingService
    {
        private readonly IAppDiscoveryService _appDiscoveryService;
        private readonly IPackageManager _packageManager;
        private readonly ILogService _logService;
        private readonly ConcurrentDictionary<string, bool> _statusCache = new();

        public AppLoadingService(
            IAppDiscoveryService appDiscoveryService,
            IPackageManager packageManager,
            ILogService logService)
        {
            _appDiscoveryService = appDiscoveryService;
            _packageManager = packageManager;
            _logService = logService;
        }

        /// <inheritdoc/>
        public async Task<OperationResult<IEnumerable<AppInfo>>> LoadAppsAsync()
        {
            try
            {
                var apps = await _appDiscoveryService.GetStandardAppsAsync();
                return OperationResult<IEnumerable<AppInfo>>.Succeeded(apps);
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to load standard apps", ex);
                return OperationResult<IEnumerable<AppInfo>>.Failed("Failed to load standard apps", ex);
            }
        }

        // Removed LoadInstallableAppsAsync as it's not in the interface

        public async Task<IEnumerable<CapabilityInfo>> LoadCapabilitiesAsync()
        {
            try
            {
                // This is a placeholder implementation
                // In a real implementation, this would query the system for available capabilities
                _logService.LogInformation("Loading Windows capabilities");

                // Return an empty list for now
                return Enumerable.Empty<CapabilityInfo>();
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to load capabilities", ex);
                return Enumerable.Empty<CapabilityInfo>();
            }
        }

        public async Task<bool> GetItemInstallStatusAsync(IInstallableItem item)
        {
            ValidationHelper.NotNull(item, nameof(item));
            ValidationHelper.NotNullOrEmpty(item.PackageId, nameof(item.PackageId));

            if (_statusCache.TryGetValue(item.PackageId, out var cachedStatus))
            {
                return cachedStatus;
            }

            var isInstalled = await _packageManager.IsAppInstalledAsync(item.PackageId);
            _statusCache[item.PackageId] = isInstalled;
            return isInstalled;
        }

        // Added missing GetInstallStatusAsync method
        /// <inheritdoc/>
        public async Task<OperationResult<InstallStatus>> GetInstallStatusAsync(string appId)
        {
            try
            {
                ValidationHelper.NotNullOrEmpty(appId, nameof(appId));

                // Use the existing cache logic, assuming 'true' maps to Success
                if (_statusCache.TryGetValue(appId, out var cachedStatus))
                {
                    return OperationResult<InstallStatus>.Succeeded(
                        cachedStatus ? InstallStatus.Success : InstallStatus.NotFound
                    );
                }

                var isInstalled = await _packageManager.IsAppInstalledAsync(appId);
                _statusCache[appId] = isInstalled;
                return OperationResult<InstallStatus>.Succeeded(
                    isInstalled ? InstallStatus.Success : InstallStatus.NotFound
                );
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to get installation status for app {appId}", ex);
                return OperationResult<InstallStatus>.Failed($"Failed to get installation status: {ex.Message}", ex);
            }
        }

        // Added missing SetInstallStatusAsync method
        /// <inheritdoc/>
        public Task<OperationResult<bool>> SetInstallStatusAsync(string appId, InstallStatus status)
        {
            try
            {
                ValidationHelper.NotNullOrEmpty(appId, nameof(appId));
                // This service primarily reads status; setting might not be its responsibility
                // or might require interaction with the package manager.
                // For now, just update the cache.
                _logService.LogWarning($"Attempting to set install status for {appId} to {status} (cache only).");
                _statusCache[appId] = (status == InstallStatus.Success); // Corrected enum member
                return Task.FromResult(OperationResult<bool>.Succeeded(true)); // Assume cache update is always successful
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to set installation status for app {appId}", ex);
                return Task.FromResult(OperationResult<bool>.Failed($"Failed to set installation status: {ex.Message}", ex));
            }
        }


        public async Task<Dictionary<string, bool>> GetBatchInstallStatusAsync(IEnumerable<string> packageIds)
        {
            ValidationHelper.NotNull(packageIds, nameof(packageIds));

            var distinctIds = packageIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (distinctIds.Count == 0)
                throw new ArgumentException("Must provide at least one valid package ID", nameof(packageIds));
            var results = new Dictionary<string, bool>();

            foreach (var id in distinctIds)
            {
                if (_statusCache.TryGetValue(id, out var cachedStatus))
                {
                    results[id] = cachedStatus;
                }
                else
                {
                    var isInstalled = await _packageManager.IsAppInstalledAsync(id);
                    _statusCache[id] = isInstalled;
                    results[id] = isInstalled;
                }
            }

            return results;
        } // Removed extra closing brace here


        /// <inheritdoc/>
        public async Task<OperationResult<bool>> RefreshInstallationStatusAsync(IEnumerable<AppInfo> apps)
        {
            try
            {
                ValidationHelper.NotNull(apps, nameof(apps));

                var packageIds = apps
                    .Where(app => app != null && !string.IsNullOrWhiteSpace(app.PackageID)) // Use PackageID from AppInfo
                    .Select(app => app.PackageID)
                    .Distinct();

                if (!packageIds.Any())
                {
                    return OperationResult<bool>.Succeeded(true); // No apps to refresh
                }

                var statuses = await GetBatchInstallStatusAsync(packageIds);

                foreach (var app in apps) // Iterate through AppInfo
                {
                    if (app != null && statuses.TryGetValue(app.PackageID, out var isInstalled)) // Use PackageID
                    {
                        _statusCache[app.PackageID] = isInstalled; // Use PackageID
                        // Optionally update the IsInstalled property on the AppInfo object itself
                        // app.IsInstalled = isInstalled; // This depends if AppInfo is mutable and if this side-effect is desired
                    }
                }

                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to refresh installation status", ex);
                return OperationResult<bool>.Failed("Failed to refresh installation status", ex);
            }
        }

        public void ClearStatusCache()
        {
            _statusCache.Clear();
        }
    }
}
