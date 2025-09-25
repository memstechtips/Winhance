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

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppLoadingService(
    IWindowsAppsService windowsAppsService,
    IExternalAppsService externalAppsService,
    IAppStatusDiscoveryService statusDiscoveryService,
    ILogService logService) : IAppLoadingService
{
    private readonly ConcurrentDictionary<string, bool> _statusCache = new();

    public async Task<OperationResult<IEnumerable<ItemDefinition>>> LoadAppsAsync()
    {
        try
        {
            var windowsApps = await windowsAppsService.GetAppsAsync();
            var externalApps = await externalAppsService.GetAppsAsync();
            var allApps = windowsApps.Concat(externalApps).ToList();

            var installStates = await statusDiscoveryService.GetInstallationStatusBatchAsync(allApps);

            foreach (var app in allApps)
            {
                app.IsInstalled = installStates.TryGetValue(app.Id, out var isInstalled) && isInstalled;
            }

            return OperationResult<IEnumerable<ItemDefinition>>.Succeeded(allApps);
        }
        catch (Exception ex)
        {
            logService.LogError("Failed to load apps", ex);
            return OperationResult<IEnumerable<ItemDefinition>>.Failed("Failed to load apps", ex);
        }
    }

    public async Task<ItemDefinition?> GetAppByIdAsync(string appId)
    {
        var windowsApps = await windowsAppsService.GetAppsAsync();
        var externalApps = await externalAppsService.GetAppsAsync();
        return windowsApps.Concat(externalApps).FirstOrDefault(app => app.Id == appId);
    }

    public async Task<IEnumerable<ItemDefinition>> LoadCapabilitiesAsync()
    {
        try
        {
            var windowsApps = await windowsAppsService.GetAppsAsync();
            return windowsApps.Where(app => !string.IsNullOrEmpty(app.CapabilityName));
        }
        catch (Exception ex)
        {
            logService.LogError("Failed to load capabilities", ex);
            return Enumerable.Empty<ItemDefinition>();
        }
    }

    public async Task<bool> GetItemInstallStatusAsync(ItemDefinition item)
    {
        ValidationHelper.NotNull(item, nameof(item));

        if (_statusCache.TryGetValue(item.Id, out var cachedStatus))
        {
            return cachedStatus;
        }

        var statusResults = await statusDiscoveryService.GetInstallationStatusBatchAsync([item]);
        var isInstalled = statusResults.GetValueOrDefault(item.Id, false);
        _statusCache[item.Id] = isInstalled;
        return isInstalled;
    }

    public async Task<OperationResult<InstallStatus>> GetInstallStatusAsync(string appId)
    {
        try
        {
            ValidationHelper.NotNullOrEmpty(appId, nameof(appId));

            if (_statusCache.TryGetValue(appId, out var cachedStatus))
            {
                return OperationResult<InstallStatus>.Succeeded(
                    cachedStatus ? InstallStatus.Success : InstallStatus.NotFound
                );
            }

            var statusResults = await statusDiscoveryService.GetInstallationStatusByIdAsync([appId]);
            var isInstalled = statusResults.GetValueOrDefault(appId, false);
            _statusCache[appId] = isInstalled;
            return OperationResult<InstallStatus>.Succeeded(
                isInstalled ? InstallStatus.Success : InstallStatus.NotFound
            );
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to get installation status for app {appId}", ex);
            return OperationResult<InstallStatus>.Failed($"Failed to get installation status: {ex.Message}", ex);
        }
    }

    public Task<OperationResult<bool>> SetInstallStatusAsync(string appId, InstallStatus status)
    {
        try
        {
            ValidationHelper.NotNullOrEmpty(appId, nameof(appId));
            _statusCache[appId] = (status == InstallStatus.Success);
            return Task.FromResult(OperationResult<bool>.Succeeded(true));
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to set installation status for app {appId}", ex);
            return Task.FromResult(OperationResult<bool>.Failed($"Failed to set installation status: {ex.Message}", ex));
        }
    }


    public async Task<Dictionary<string, bool>> GetBatchInstallStatusAsync(IEnumerable<ItemDefinition> definitions)
    {
        ValidationHelper.NotNull(definitions, nameof(definitions));

        var definitionList = definitions.ToList();

        if (definitionList.Count == 0)
            throw new ArgumentException("Must provide at least one valid definition", nameof(definitions));

        return await statusDiscoveryService.GetInstallationStatusBatchAsync(definitionList);
    }

    private static string GetKeyForDefinition(ItemDefinition definition)
    {
        return definition.CapabilityName ?? definition.OptionalFeatureName ?? definition.AppxPackageName ?? definition.Id;
    }

    public async Task<OperationResult<bool>> RefreshInstallationStatusAsync(IEnumerable<ItemDefinition> apps)
    {
        try
        {
            ValidationHelper.NotNull(apps, nameof(apps));

            var appsList = apps.Where(app => app != null).ToList();

            if (!appsList.Any())
            {
                return OperationResult<bool>.Succeeded(true);
            }

            var statuses = await GetBatchInstallStatusAsync(appsList);

            foreach (var app in appsList)
            {
                var key = GetKeyForDefinition(app);
                if (statuses.TryGetValue(key, out var isInstalled))
                {
                    _statusCache[key] = isInstalled;
                    app.IsInstalled = isInstalled;
                }
            }

            return OperationResult<bool>.Succeeded(true);
        }
        catch (Exception ex)
        {
            logService.LogError("Failed to refresh installation status", ex);
            return OperationResult<bool>.Failed("Failed to refresh installation status", ex);
        }
    }

    public void ClearStatusCache()
    {
        _statusCache.Clear();
    }
}
