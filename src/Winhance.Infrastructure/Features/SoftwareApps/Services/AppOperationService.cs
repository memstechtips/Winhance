using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppOperationService(
    IWinGetService winGetService,
    ILegacyCapabilityService capabilityService,
    IOptionalFeatureService featureService,
    IAppLoadingService appLoadingService,
    ILogService logService,
    IEventBus eventBus,
    IWindowsAppsService windowsAppsService,
    IBloatRemovalService bloatRemovalService) : IAppOperationService
{
    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            await bloatRemovalService.RemoveItemsFromScriptAsync(new List<ItemDefinition> { app });
            return await InstallSingleAppAsync(app, progress);
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install app '{app?.Id}': {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    private async Task<OperationResult<bool>> InstallSingleAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(app?.CapabilityName))
            {
                var launched = await capabilityService.EnableCapabilityAsync(app.CapabilityName, app.Name);
                if (launched)
                {
                    eventBus.Publish(new AppInstalledEvent(app.Id));
                    logService.Log(LogLevel.Info, $"PowerShell launched for capability '{app.Id}'");
                    return OperationResult<bool>.Succeeded(true);
                }
                return OperationResult<bool>.Failed("Failed to launch PowerShell for capability");
            }

            if (!string.IsNullOrEmpty(app?.OptionalFeatureName))
            {
                var launched = await featureService.EnableFeatureAsync(app.OptionalFeatureName, app.Name);
                if (launched)
                {
                    eventBus.Publish(new AppInstalledEvent(app.Id));
                    logService.Log(LogLevel.Info, $"PowerShell launched for feature '{app.Id}'");
                    return OperationResult<bool>.Succeeded(true);
                }
                return OperationResult<bool>.Failed("Failed to launch PowerShell for feature");
            }

            if (!string.IsNullOrEmpty(app?.WinGetPackageId))
            {
                var success = await winGetService.InstallPackageAsync(app.WinGetPackageId, app.Name, CancellationToken.None);
                if (success)
                {
                    eventBus.Publish(new AppInstalledEvent(app.Id));
                    logService.Log(LogLevel.Success, $"Successfully installed app '{app.Id}'");
                }
                return success ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("Installation failed");
            }

            return OperationResult<bool>.Failed($"App '{app?.Id}' not supported for installation");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install app '{app?.Id}': {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> UninstallAppAsync(string appId, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            var app = await windowsAppsService.GetAppByIdAsync(appId);
            if (app == null)
                return OperationResult<bool>.Failed("App not found");

            // Route to appropriate service based on app type (like InstallAppAsync does)
            if (!string.IsNullOrEmpty(app.CapabilityName))
            {
                var success = await capabilityService.DisableCapabilityAsync(app.CapabilityName, app.Name);
                if (success)
                {
                    eventBus.Publish(new AppRemovedEvent(appId));
                    logService.Log(LogLevel.Success, $"Successfully removed capability '{appId}'");
                }
                return success ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("Capability removal failed");
            }

            if (!string.IsNullOrEmpty(app.OptionalFeatureName))
            {
                var success = await featureService.DisableFeatureAsync(app.OptionalFeatureName, app.Name);
                if (success)
                {
                    eventBus.Publish(new AppRemovedEvent(appId));
                    logService.Log(LogLevel.Success, $"Successfully removed feature '{appId}'");
                }
                return success ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("Feature removal failed");
            }

            // For AppX packages, use bloatRemovalService
            if (!string.IsNullOrEmpty(app.AppxPackageName))
            {
                var success = await bloatRemovalService.RemoveAppsAsync(new[] { app }.ToList(), progress);
                if (success)
                {
                    eventBus.Publish(new AppRemovedEvent(appId));
                    logService.Log(LogLevel.Success, $"Successfully removed app '{appId}'");
                }
                return success ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("App removal failed");
            }

            return OperationResult<bool>.Failed($"App '{appId}' not supported for removal");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to remove app '{appId}': {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<int>> InstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (apps == null || !apps.Any())
                return OperationResult<int>.Failed("No apps provided");

            await bloatRemovalService.RemoveItemsFromScriptAsync(apps);

            int successCount = 0;
            foreach (var app in apps)
            {
                var result = await InstallSingleAppAsync(app, progress);
                if (result.Success) successCount++;
            }

            return OperationResult<int>.Succeeded(successCount);
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install apps: {ex.Message}");
            return OperationResult<int>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<int>> UninstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (apps == null || !apps.Any())
                return OperationResult<int>.Failed("No apps provided");

            var success = await bloatRemovalService.RemoveAppsAsync(apps, progress);

            if (success)
            {
                foreach (var app in apps)
                {
                    eventBus.Publish(new AppRemovedEvent(app.Id));
                }
                logService.Log(LogLevel.Success, $"Successfully removed {apps.Count} apps");
                return OperationResult<int>.Succeeded(apps.Count);
            }

            return OperationResult<int>.Failed("Bulk removal failed");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to remove apps: {ex.Message}");
            return OperationResult<int>.Failed(ex.Message);
        }
    }
}

public class AppInstalledEvent(string appId) : IDomainEvent
{
    public string AppId { get; } = appId;
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public class AppRemovedEvent(string appId) : IDomainEvent
{
    public string AppId { get; } = appId;
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}