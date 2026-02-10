using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
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
    IExternalAppsService externalAppsService,
    IBloatRemovalService bloatRemovalService,
    IScheduledTaskService scheduledTaskService,
    ITaskProgressService taskProgressService,
    IWindowsRegistryService registryService) : IAppOperationService
{
    private CancellationToken GetCurrentCancellationToken()
    {
        return taskProgressService?.CurrentTaskCancellationSource?.Token ?? CancellationToken.None;
    }
    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true)
    {
        try
        {
            if (shouldRemoveFromBloatScript)
            {
                await bloatRemovalService.RemoveItemsFromScriptAsync(new List<ItemDefinition> { app });
                await CleanupDedicatedRemovalArtifactsAsync(app);
            }
            return await InstallSingleAppAsync(app, progress);
        }
        catch (OperationCanceledException)
        {
            logService.Log(LogLevel.Info, $"Installation of '{app?.Id}' was cancelled");
            return OperationResult<bool>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install app '{app?.Id}': {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    private async Task<OperationResult<bool>> InstallSingleAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = GetCurrentCancellationToken();

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

            if ((app?.WinGetPackageId != null && app.WinGetPackageId.Any()) ||
                (app?.CustomProperties?.ContainsKey("RequiresDirectDownload") == true))
            {
                // Check if this is a Windows Store app (has AppxPackageName) or External app
                bool isWindowsStoreApp = !string.IsNullOrEmpty(app.AppxPackageName);

                if (isWindowsStoreApp)
                {
                    // Use WindowsAppsService which has fallback logic for market-restricted Microsoft Store apps
                    var result = await windowsAppsService.InstallAppAsync(app, progress);
                    if (result.Success)
                    {
                        eventBus.Publish(new AppInstalledEvent(app.Id));
                        logService.Log(LogLevel.Success, $"Successfully installed app '{app.Id}'");
                    }
                    return result;
                }
                else
                {
                    // External apps - route through ExternalAppsService which handles both WinGet and direct downloads
                    var result = await externalAppsService.InstallAppAsync(app, progress);
                    if (result.Success)
                    {
                        eventBus.Publish(new AppInstalledEvent(app.Id));
                        logService.Log(LogLevel.Success, $"Successfully installed app '{app.Id}'");
                    }
                    return result;
                }
            }

            return OperationResult<bool>.Failed($"App '{app?.Id}' not supported for installation");
        }
        catch (OperationCanceledException)
        {
            logService.Log(LogLevel.Info, $"Installation of '{app?.Id}' was cancelled");
            return OperationResult<bool>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install app '{app?.Id}': {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> UninstallAppAsync(string appId, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = GetCurrentCancellationToken();
        
        try
        {
            var app = await windowsAppsService.GetAppByIdAsync(appId);
            if (app == null)
                return OperationResult<bool>.Failed("App not found");

            if (!string.IsNullOrEmpty(app.CapabilityName) ||
                !string.IsNullOrEmpty(app.OptionalFeatureName) ||
                !string.IsNullOrEmpty(app.AppxPackageName))
            {
                var success = await bloatRemovalService.RemoveAppsAsync(new[] { app }.ToList(), progress, cancellationToken);
                if (success)
                {
                    eventBus.Publish(new AppRemovedEvent(appId));
                    logService.Log(LogLevel.Success, $"Successfully removed '{appId}'");
                }
                return success ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("Removal failed");
            }

            return OperationResult<bool>.Failed($"App '{appId}' not supported for removal");
        }
        catch (OperationCanceledException)
        {
            logService.Log(LogLevel.Info, $"Removal of '{appId}' was cancelled");
            return OperationResult<bool>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to remove app '{appId}': {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<int>> InstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true)
    {
        try
        {
            if (apps == null || !apps.Any())
                return OperationResult<int>.Failed("No apps provided");

            if (shouldRemoveFromBloatScript)
            {
                await bloatRemovalService.RemoveItemsFromScriptAsync(apps);

                foreach (var app in apps)
                {
                    await CleanupDedicatedRemovalArtifactsAsync(app);
                }
            }

            int successCount = 0;
            foreach (var app in apps)
            {
                var result = await InstallSingleAppAsync(app, progress);
                if (result.Success) successCount++;
            }

            return OperationResult<int>.Succeeded(successCount);
        }
        catch (OperationCanceledException)
        {
            logService.Log(LogLevel.Info, "Bulk installation was cancelled");
            return OperationResult<int>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install apps: {ex.Message}");
            return OperationResult<int>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<int>> UninstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null, bool saveRemovalScripts = true)
    {
        var cancellationToken = GetCurrentCancellationToken();

        try
        {
            if (apps == null || !apps.Any())
                return OperationResult<int>.Failed("No apps provided");

            var success = await bloatRemovalService.RemoveAppsAsync(apps, progress, cancellationToken, saveRemovalScripts);

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
        catch (OperationCanceledException)
        {
            logService.Log(LogLevel.Info, "Bulk removal was cancelled");
            return OperationResult<int>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to remove apps: {ex.Message}");
            return OperationResult<int>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> UninstallExternalAppAsync(string packageId, string displayName, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = GetCurrentCancellationToken();

        try
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return OperationResult<bool>.Failed("Package ID cannot be null or empty");

            var item = new ItemDefinition
            {
                Id = packageId,
                Name = displayName,
                Description = string.Empty,
                WinGetPackageId = [packageId]
            };
            var result = await externalAppsService.UninstallAppAsync(item, progress);

            if (result.Success)
            {
                eventBus.Publish(new AppRemovedEvent(packageId));
                logService.Log(LogLevel.Success, $"Successfully uninstalled '{displayName}'");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            logService.Log(LogLevel.Info, $"Uninstallation of '{displayName}' was cancelled");
            return OperationResult<bool>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to uninstall '{displayName}': {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<int>> UninstallExternalAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = GetCurrentCancellationToken();

        try
        {
            if (apps == null || !apps.Any())
                return OperationResult<int>.Failed("No apps provided");

            int successCount = 0;
            int totalCount = apps.Count;

            foreach (var app in apps)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logService.Log(LogLevel.Info, "Bulk uninstallation was cancelled");
                    return OperationResult<int>.Cancelled($"Cancelled after {successCount} of {totalCount} apps");
                }

                try
                {
                    var result = await externalAppsService.UninstallAppAsync(app, progress);

                    if (result.Success)
                    {
                        successCount++;
                        eventBus.Publish(new AppRemovedEvent(app.Id));
                    }
                }
                catch (OperationCanceledException)
                {
                    return OperationResult<int>.Cancelled($"Cancelled after {successCount} of {totalCount} apps");
                }
                catch (Exception ex)
                {
                    logService.LogError($"Failed to uninstall {app.Name}: {ex.Message}");
                }
            }

            if (successCount == totalCount)
            {
                logService.Log(LogLevel.Success, $"Successfully uninstalled all {totalCount} apps");
                return OperationResult<int>.Succeeded(successCount);
            }
            else if (successCount > 0)
            {
                logService.Log(LogLevel.Warning, $"Partially completed: {successCount} of {totalCount} apps uninstalled");
                return OperationResult<int>.Succeeded(successCount);
            }
            else
            {
                return OperationResult<int>.Failed("Failed to uninstall any apps");
            }
        }
        catch (OperationCanceledException)
        {
            logService.Log(LogLevel.Info, "Bulk uninstallation was cancelled");
            return OperationResult<int>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to uninstall apps: {ex.Message}");
            return OperationResult<int>.Failed(ex.Message);
        }
    }

    private async Task CleanupDedicatedRemovalArtifactsAsync(ItemDefinition app)
    {
        if (app.Id == "windows-app-edge" || app.Id == "windows-app-onedrive")
        {
            var scriptName = CreateScriptName(app.Id);
            var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, scriptName);
            var taskName = scriptName.Replace(".ps1", "");

            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
                logService.LogInformation($"Deleted obsolete script: {scriptPath}");
            }

            await scheduledTaskService.UnregisterScheduledTaskAsync(taskName);
            logService.LogInformation($"Cleaned up artifacts for reinstalled app: {app.Id}");

            if (app.Id == "windows-app-edge")
            {
                await CleanupOpenWebSearchAsync();
            }
        }
    }

    private static string CreateScriptName(string appId)
    {
        return appId switch
        {
            "windows-app-edge" => "EdgeRemoval.ps1",
            "windows-app-onedrive" => "OneDriveRemoval.ps1",
            _ => throw new NotSupportedException($"No dedicated script defined for {appId}")
        };
    }

    private async Task CleanupOpenWebSearchAsync()
    {
        try
        {
            logService.LogInformation("Cleaning up OpenWebSearch installation...");

            await scheduledTaskService.UnregisterScheduledTaskAsync("OpenWebSearchRepair");
            logService.LogInformation("Removed OpenWebSearchRepair scheduled task");

            var openWebSearchDir = @"C:\ProgramData\Winhance\OpenWebSearch";
            if (Directory.Exists(openWebSearchDir))
            {
                Directory.Delete(openWebSearchDir, recursive: true);
                logService.LogInformation($"Deleted directory: {openWebSearchDir}");
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var edgeHardlink = Path.Combine(programFilesX86, @"Microsoft\Edge\Application\edge.exe");
            if (File.Exists(edgeHardlink))
            {
                File.Delete(edgeHardlink);
                logService.LogInformation($"Deleted Edge hardlink: {edgeHardlink}");
            }

            CleanupOpenWebSearchRegistry();

            logService.LogInformation("OpenWebSearch cleanup completed successfully");
        }
        catch (Exception ex)
        {
            logService.LogError($"Error cleaning up OpenWebSearch: {ex.Message}");
        }
    }

    private void CleanupOpenWebSearchRegistry()
    {
        try
        {
            var ifeoBasePath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

            registryService.DeleteKey($@"{ifeoBasePath}\ie_to_edge_stub.exe");
            registryService.DeleteKey($@"{ifeoBasePath}\msedge.exe");

            logService.LogInformation("Removed IFEO debugger entries");

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var msedgePath = Path.Combine(programFilesX86, @"Microsoft\Edge\Application\msedge.exe");

            if (File.Exists(msedgePath))
            {
                registryService.DeleteValue(@"HKEY_CLASSES_ROOT\microsoft-edge", "NoOpenWith");
                registryService.DeleteValue(@"HKEY_CLASSES_ROOT\MSEdgeHTM", "NoOpenWith");

                var edgeCommand = $"\"{msedgePath}\" --single-argument %1";
                registryService.SetValue(@"HKEY_CLASSES_ROOT\microsoft-edge\shell\open\command", "", edgeCommand, RegistryValueKind.String);
                registryService.SetValue(@"HKEY_CLASSES_ROOT\MSEdgeHTM\shell\open\command", "", edgeCommand, RegistryValueKind.String);

                logService.LogInformation("Restored Edge protocol handlers");
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Error cleaning up OpenWebSearch registry: {ex.Message}");
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