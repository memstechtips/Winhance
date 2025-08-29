using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Exceptions;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    public class InstallationStatusService : IInstallationStatusService
    {
        private readonly IPackageManager _packageManager;
        private readonly ConcurrentDictionary<string, bool> _statusCache = new();
        private readonly ILogService _logService;

        public InstallationStatusService(IPackageManager packageManager, ILogService logService)
        {
            _packageManager = packageManager;
            _logService = logService;
        }

        public Task<bool> SetInstallStatusAsync(string appId, InstallStatus status)
        {
            try
            {
                _logService.LogInformation($"Setting install status for {appId} to {status}");
                bool isInstalled = status == InstallStatus.Success;
                _statusCache.AddOrUpdate(appId, isInstalled, (k, v) => isInstalled);
                _logService.LogInformation($"Successfully set status for {appId}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to set install status for {appId}", ex);
                return Task.FromResult(false);
            }
        }

        public async Task<InstallStatus> GetInstallStatusAsync(string appId)
        {
            try
            {
                if (_statusCache.TryGetValue(appId, out var cachedStatus))
                {
                    _logService.LogInformation($"Retrieved cached status for {appId}");
                    return cachedStatus ? InstallStatus.Success : InstallStatus.Failed;
                }

                _logService.LogInformation($"Querying package manager for {appId}");
                var isInstalled = await _packageManager.IsAppInstalledAsync(appId);
                _statusCache.TryAdd(appId, isInstalled);

                _logService.LogInformation($"Install status for {appId}: {isInstalled}");
                return isInstalled ? InstallStatus.Success : InstallStatus.Failed;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to get install status for {appId}", ex);
                return InstallStatus.Failed;
            }
        }

        public async Task<RefreshResult> RefreshStatusAsync(IEnumerable<string> appIds)
        {
            var result = new RefreshResult();
            var errors = new Dictionary<string, string>();

            try
            {
                _logService.LogInformation("Refreshing installation status for batch of apps");

                foreach (var appId in appIds.Distinct())
                {
                    try
                    {
                        var isInstalled = await _packageManager.IsAppInstalledAsync(appId);
                        _statusCache.AddOrUpdate(appId, isInstalled, (k, v) => isInstalled);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        errors[appId] = ex.Message;
                        result.FailedCount++;
                        _logService.LogError($"Failed to refresh status for {appId}", ex);
                    }
                }

                result.Errors = errors;
                _logService.LogSuccess("Successfully refreshed installation status");
                return result;
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to refresh installation status", ex);
                throw new InstallationStatusException("Failed to refresh installation status", ex);
            }
        }

        public async Task RefreshInstallationStatusAsync(
            IEnumerable<IInstallableItem> items,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logService.LogInformation("Refreshing installation status for batch of items");

                var packageIds = items.Select(i => i.PackageId).Distinct();
                var statuses = await GetBatchInstallStatusAsync(packageIds, cancellationToken);

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (statuses.TryGetValue(item.PackageId, out var isInstalled))
                    {
                        _statusCache.TryAdd(item.PackageId, isInstalled);
                    }
                }

                _logService.LogSuccess("Successfully refreshed installation status");
            }
            catch (OperationCanceledException)
            {
                _logService.LogWarning("Installation status refresh was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to refresh installation status", ex);
                throw new InstallationStatusException("Failed to refresh installation status", ex);
            }
        }

        public async Task<bool> GetItemInstallStatusAsync(
            IInstallableItem item,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (_statusCache.TryGetValue(item.PackageId, out var cachedStatus))
                {
                    _logService.LogInformation($"Retrieved cached status for {item.PackageId}");
                    return cachedStatus;
                }

                _logService.LogInformation($"Querying package manager for {item.PackageId}");
                var isInstalled = await _packageManager.IsAppInstalledAsync(item.PackageId, cancellationToken);
                _statusCache.TryAdd(item.PackageId, isInstalled);

                _logService.LogInformation($"Install status for {item.PackageId}: {isInstalled}");
                return isInstalled;
            }
            catch (OperationCanceledException)
            {
                _logService.LogWarning($"Install status check for {item.PackageId} was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to get install status for {item.PackageId}", ex);
                throw new InstallationStatusException($"Failed to get install status for {item.PackageId}", ex);
            }
        }

        public async Task<Dictionary<string, bool>> GetBatchInstallStatusAsync(
            IEnumerable<string> packageIds,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var distinctIds = packageIds.Distinct().ToList();
                var results = new Dictionary<string, bool>();
                _logService.LogInformation($"Getting batch install status for {distinctIds.Count} packages");

                foreach (var id in distinctIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_statusCache.TryGetValue(id, out var cachedStatus))
                    {
                        results[id] = cachedStatus;
                        _logService.LogInformation($"Using cached status for {id}");
                    }
                    else
                    {
                        var isInstalled = await _packageManager.IsAppInstalledAsync(id, cancellationToken);
                        _statusCache.TryAdd(id, isInstalled);
                        results[id] = isInstalled;
                        _logService.LogInformation($"Retrieved fresh status for {id}: {isInstalled}");
                    }
                }

                _logService.LogSuccess("Completed batch install status check");
                return results;
            }
            catch (OperationCanceledException)
            {
                _logService.LogWarning("Batch install status check was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to get batch install status", ex);
                throw new InstallationStatusException("Failed to get batch install status", ex);
            }
        }

        public void ClearStatusCache()
        {
            try
            {
                _logService.LogInformation("Clearing installation status cache");
                _statusCache.Clear();
                _logService.LogSuccess("Successfully cleared installation status cache");
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to clear installation status cache", ex);
                throw new InstallationStatusException("Failed to clear installation status cache", ex);
            }
        }
    }
}
