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
            var windowsApps = await windowsAppsService.GetAppsAsync().ConfigureAwait(false);
            var externalApps = await externalAppsService.GetAppsAsync().ConfigureAwait(false);
            var allApps = windowsApps.Concat(externalApps).ToList();

            var installStates = await statusDiscoveryService.GetInstallationStatusBatchAsync(allApps).ConfigureAwait(false);

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
        var windowsApps = await windowsAppsService.GetAppsAsync().ConfigureAwait(false);
        var externalApps = await externalAppsService.GetAppsAsync().ConfigureAwait(false);
        return windowsApps.Concat(externalApps).FirstOrDefault(app => app.Id == appId);
    }

    private async Task<Dictionary<string, bool>> GetBatchInstallStatusAsync(IEnumerable<ItemDefinition> definitions)
    {
        ValidationHelper.NotNull(definitions, nameof(definitions));

        var definitionList = definitions.ToList();

        if (definitionList.Count == 0)
            throw new ArgumentException("Must provide at least one valid definition", nameof(definitions));

        return await statusDiscoveryService.GetInstallationStatusBatchAsync(definitionList).ConfigureAwait(false);
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

            var statuses = await GetBatchInstallStatusAsync(appsList).ConfigureAwait(false);

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

}
