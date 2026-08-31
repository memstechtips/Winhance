using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class AppInstallationService(
    ILegacyCapabilityService capabilityService,
    IOptionalFeatureService featureService,
    IServicingSession servicingSession,
    ILogService logService,
    IWindowsAppsService windowsAppsService,
    IExternalAppsService externalAppsService,
    IBloatRemovalService bloatRemovalService,
    IScheduledTaskService scheduledTaskService,
    ITaskProgressService taskProgressService,
    IFileSystemService fileSystemService,
    IChangeHistoryService changeHistory) : IAppInstallationService
{
    // Enabling a feature or capability is handed to a PowerShell window Winhance does not watch, so
    // every servicing path reports this rather than an install it cannot vouch for.
    private const string HandedOffMessage = "Enable started in a separate window; success can only be determined there";

    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true)
    {
        try
        {
            if (shouldRemoveFromBloatScript)
            {
                await bloatRemovalService.RemoveItemsFromScriptAsync(new List<ItemDefinition> { app }).ConfigureAwait(false);
                await CleanupDedicatedRemovalArtifactsAsync(app).ConfigureAwait(false);
            }
            return await InstallSingleAppAsync(app, progress).ConfigureAwait(false);
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

    public async Task<OperationResult<int>> InstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true)
    {
        try
        {
            if (apps == null || apps.Count == 0)
                return OperationResult<int>.Failed("No apps provided");

            if (shouldRemoveFromBloatScript)
            {
                await bloatRemovalService.RemoveItemsFromScriptAsync(apps).ConfigureAwait(false);

                foreach (var app in apps)
                {
                    await CleanupDedicatedRemovalArtifactsAsync(app).ConfigureAwait(false);
                }
            }

            int successCount = 0;
            foreach (var app in apps)
            {
                var result = await InstallSingleAppAsync(app, progress).ConfigureAwait(false);
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

    public async Task<OperationResult<bool>> EnableServicingBatchAsync(
        IReadOnlyList<ItemDefinition> apps,
        IProgress<TaskProgressDetail>? progress = null,
        bool shouldRemoveFromBloatScript = true)
    {
        try
        {
            var batch = apps?
                .Where(a => !string.IsNullOrEmpty(a.CapabilityName) || !string.IsNullOrEmpty(a.OptionalFeatureName))
                .ToList();
            if (batch == null || batch.Count == 0)
                return OperationResult<bool>.Failed("No apps provided");

            var cancellationToken = taskProgressService.GetCurrentCancellationToken();
            cancellationToken.ThrowIfCancellationRequested();

            if (shouldRemoveFromBloatScript)
            {
                await bloatRemovalService.RemoveItemsFromScriptAsync(batch).ConfigureAwait(false);

                foreach (var app in batch)
                {
                    await CleanupDedicatedRemovalArtifactsAsync(app).ConfigureAwait(false);
                }
            }

            // An item may carry both names; capability wins, the same way InstallSingleAppCoreAsync
            // routes a single app, so nothing is serviced twice.
            var featureNames = batch
                .Where(a => string.IsNullOrEmpty(a.CapabilityName))
                .Select(a => a.OptionalFeatureName!)
                .ToList();
            var capabilityNames = batch
                .Where(a => !string.IsNullOrEmpty(a.CapabilityName))
                .Select(a => a.CapabilityName!)
                .ToList();

            var statements = new List<string>();
            if (featureNames.Count > 0)
                statements.Add(featureService.BuildEnableStatement(featureNames));
            if (capabilityNames.Count > 0)
                statements.Add(capabilityService.BuildEnableStatement(capabilityNames));

            var launched = await servicingSession.RunAsync(
                statements,
                string.Join(", ", batch.Select(a => a.Name)),
                progress,
                cancellationToken).ConfigureAwait(false);

            if (!launched)
                return OperationResult<bool>.Failed("Failed to launch PowerShell");

            foreach (var app in batch)
            {
                changeHistory.LogAppChange(app.Name, AppChangeKind.EnableStarted);
            }

            return OperationResult<bool>.DeferredSuccess(true, HandedOffMessage);
        }
        catch (OperationCanceledException)
        {
            logService.Log(LogLevel.Info, "Enabling was cancelled");
            return OperationResult<bool>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to enable: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    private async Task<OperationResult<bool>> InstallSingleAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null)
    {
        var result = await InstallSingleAppCoreAsync(app, progress).ConfigureAwait(false);
        if (result.Success)
        {
            // An InfoMessage means another window owns the outcome, so the receipt must not claim an install.
            changeHistory.LogAppChange(app.Name, result.InfoMessage is null ? AppChangeKind.Installed : AppChangeKind.EnableStarted);
        }
        return result;
    }

    private async Task<OperationResult<bool>> InstallSingleAppCoreAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = taskProgressService.GetCurrentCancellationToken();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(app?.CapabilityName))
            {
                var launched = await capabilityService.EnableCapabilitiesAsync([app.CapabilityName], [app.Name], progress, cancellationToken).ConfigureAwait(false);
                if (launched)
                {
                    logService.Log(LogLevel.Info, $"PowerShell launched for capability '{app.Id}'");
                    return OperationResult<bool>.DeferredSuccess(true, HandedOffMessage);
                }
                return OperationResult<bool>.Failed("Failed to launch PowerShell for capability");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(app?.OptionalFeatureName))
            {
                var launched = await featureService.EnableFeaturesAsync([app.OptionalFeatureName], [app.Name], progress, cancellationToken).ConfigureAwait(false);
                if (launched)
                {
                    logService.Log(LogLevel.Info, $"PowerShell launched for feature '{app.Id}'");
                    return OperationResult<bool>.DeferredSuccess(true, HandedOffMessage);
                }
                return OperationResult<bool>.Failed("Failed to launch PowerShell for feature");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if ((app?.WinGetPackageId != null && app.WinGetPackageId.Length > 0) ||
                !string.IsNullOrEmpty(app?.MsStoreId) ||
                app?.ExternalApp?.RequiresDirectDownload == true ||
                app?.ExternalApp?.DownloadUrl != null)
            {
                bool isWindowsStoreApp = app.AppxPackageName?.Length > 0;

                if (isWindowsStoreApp)
                {
                    var result = await windowsAppsService.InstallAppAsync(app, progress).ConfigureAwait(false);
                    if (result.Success)
                    {
                        logService.Log(LogLevel.Success, $"Successfully installed app '{app.Id}'");
                    }
                    return result;
                }
                else
                {
                    var result = await externalAppsService.InstallAppAsync(app, progress).ConfigureAwait(false);
                    if (result.Success)
                    {
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

    private async Task CleanupDedicatedRemovalArtifactsAsync(ItemDefinition app)
    {
        if (app.Id == "windows-app-edge" || app.Id == "windows-app-onedrive")
        {
            var scriptName = CreateScriptName(app.Id);
            var scriptPath = fileSystemService.CombinePath(ScriptPaths.ScriptsDirectory, scriptName);
            var taskName = scriptName.Replace(".ps1", "");

            if (fileSystemService.FileExists(scriptPath))
            {
                fileSystemService.DeleteFile(scriptPath);
                logService.LogInformation($"Deleted obsolete script: {scriptPath}");
            }

            await scheduledTaskService.UnregisterScheduledTaskAsync(taskName).ConfigureAwait(false);
            logService.LogInformation($"Cleaned up artifacts for reinstalled app: {app.Id}");

            if (app.Id == "windows-app-edge")
            {
                await CleanupOpenWebSearchAsync().ConfigureAwait(false);
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

            await scheduledTaskService.UnregisterScheduledTaskAsync("OpenWebSearchRepair").ConfigureAwait(false);
            logService.LogInformation("Removed OpenWebSearchRepair scheduled task");

            var openWebSearchDir = @"C:\ProgramData\Winhance\OpenWebSearch";
            if (fileSystemService.DirectoryExists(openWebSearchDir))
            {
                fileSystemService.DeleteDirectory(openWebSearchDir, recursive: true);
                logService.LogInformation($"Deleted directory: {openWebSearchDir}");
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var edgeHardlink = fileSystemService.CombinePath(programFilesX86, @"Microsoft\Edge\Application\edge.exe");
            if (fileSystemService.FileExists(edgeHardlink))
            {
                fileSystemService.DeleteFile(edgeHardlink);
                logService.LogInformation($"Deleted Edge hardlink: {edgeHardlink}");
            }

            logService.LogInformation("OpenWebSearch cleanup completed successfully");
        }
        catch (Exception ex)
        {
            logService.LogError($"Error cleaning up OpenWebSearch: {ex.Message}");
        }
    }
}
