using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppUninstallationService(
    ILogService logService,
    IWindowsAppsService windowsAppsService,
    IBloatRemovalService bloatRemovalService,
    ITaskProgressService taskProgressService,
    IMultiScriptProgressService multiScriptProgressService) : IAppUninstallationService
{
    private CancellationToken GetCurrentCancellationToken()
    {
        return taskProgressService?.CurrentTaskCancellationSource?.Token ?? CancellationToken.None;
    }

    public async Task<OperationResult<bool>> UninstallAppAsync(string appId, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = GetCurrentCancellationToken();

        try
        {
            logService.LogInformation($"[UninstallApp] START: appId='{appId}'");
            var app = await windowsAppsService.GetAppByIdAsync(appId).ConfigureAwait(false);
            if (app == null)
            {
                logService.LogInformation($"[UninstallApp] App '{appId}' not found in definitions");
                return OperationResult<bool>.Failed("App not found");
            }

            logService.LogInformation($"[UninstallApp] Found: '{app.Name}' AppX={app.AppxPackageName ?? "null"} Cap={app.CapabilityName ?? "null"} Feat={app.OptionalFeatureName ?? "null"}");

            RemovalOutcome outcome;

            if (app.RemovalScript != null)
            {
                logService.LogInformation($"[UninstallApp] Routing to ExecuteDedicatedScriptAsync for '{app.Name}'");
                outcome = await bloatRemovalService.ExecuteDedicatedScriptAsync(app, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                logService.LogInformation($"[UninstallApp] Routing to ExecuteBloatRemovalAsync for '{app.Name}'");
                outcome = await bloatRemovalService.ExecuteBloatRemovalAsync([app], progress, cancellationToken).ConfigureAwait(false);
            }

            return outcome switch
            {
                RemovalOutcome.Success => OperationResult<bool>.Succeeded(true),
                RemovalOutcome.DeferredToScheduledTask => OperationResult<bool>.DeferredSuccess(true, "Items will be removed at next startup"),
                _ => OperationResult<bool>.Failed("Removal failed")
            };
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

    public async Task<OperationResult<int>> UninstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null, bool saveRemovalScripts = true)
    {
        var cancellationToken = GetCurrentCancellationToken();

        try
        {
            if (apps == null || !apps.Any())
                return OperationResult<int>.Failed("No apps provided");

            logService.LogInformation($"[UninstallApps] START: {apps.Count} total apps to process");

            var scriptApps = apps.Where(a => a.RemovalScript != null).ToList();
            var regularApps = apps.Where(a => a.RemovalScript == null).ToList();

            logService.LogInformation($"[UninstallApps] Categorization: ScriptApps={scriptApps.Count}, RegularApps={regularApps.Count}");
            foreach (var a in apps)
                logService.LogInformation($"[UninstallApps]   Item: '{a.Name}' Id={a.Id} AppX={a.AppxPackageName ?? "null"} Cap={a.CapabilityName ?? "null"} Feat={a.OptionalFeatureName ?? "null"} IsInstalled={a.IsInstalled}");

            var anyDeferred = false;

            if (scriptApps.Count > 0)
            {
                logService.LogInformation($"[UninstallApps] Step 2: Executing {scriptApps.Count} dedicated scripts...");
                foreach (var app in scriptApps)
                {
                    var outcome = await bloatRemovalService.ExecuteDedicatedScriptAsync(app, progress, cancellationToken).ConfigureAwait(false);
                    if (outcome == RemovalOutcome.DeferredToScheduledTask) anyDeferred = true;
                }
                logService.LogInformation("[UninstallApps] Step 2 DONE");
            }

            if (regularApps.Count > 0)
            {
                logService.LogInformation($"[UninstallApps] Step 3: Executing BloatRemoval for {regularApps.Count} regular apps...");
                var outcome = await bloatRemovalService.ExecuteBloatRemovalAsync(regularApps, progress, cancellationToken).ConfigureAwait(false);
                if (outcome == RemovalOutcome.DeferredToScheduledTask) anyDeferred = true;
                logService.LogInformation("[UninstallApps] Step 3 DONE");
            }

            if (saveRemovalScripts)
            {
                logService.LogInformation("[UninstallApps] Step 4: Persisting removal scripts...");
                await bloatRemovalService.PersistRemovalScriptsAsync(apps).ConfigureAwait(false);
            }
            else
            {
                logService.LogInformation("[UninstallApps] Step 4: Cleaning up all removal artifacts...");
                await bloatRemovalService.CleanupAllRemovalArtifactsAsync().ConfigureAwait(false);
            }
            logService.LogInformation("[UninstallApps] Step 4 DONE");

            if (anyDeferred)
            {
                logService.LogWarning($"[UninstallApps] DONE: {apps.Count} apps deferred to scheduled task");
                return OperationResult<int>.DeferredSuccess(apps.Count, "Items will be removed at next startup");
            }

            logService.Log(LogLevel.Success, $"[UninstallApps] DONE: Successfully removed {apps.Count} apps");
            return OperationResult<int>.Succeeded(apps.Count);
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

    public async Task<OperationResult<int>> UninstallAppsInParallelAsync(List<ItemDefinition> apps, bool saveRemovalScripts = true)
    {
        try
        {
            if (apps == null || !apps.Any())
                return OperationResult<int>.Failed("No apps provided");

            logService.LogInformation($"[UninstallAppsParallel] START: {apps.Count} total apps to process");

            var scriptApps = apps.Where(a => a.RemovalScript != null).ToList();
            var regularApps = apps.Where(a => a.RemovalScript == null).ToList();

            logService.LogInformation($"[UninstallAppsParallel] ScriptApps={scriptApps.Count}, RegularApps={regularApps.Count}");

            var slotNames = new List<string>();
            foreach (var app in scriptApps)
                slotNames.Add(GetScriptSlotName(app));
            if (regularApps.Count > 0)
                slotNames.Add("BloatRemoval");

            var cts = multiScriptProgressService.StartMultiScriptTask(slotNames.ToArray());
            var cancellationToken = cts.Token;

            try
            {
                var progressReporters = new List<IProgress<TaskProgressDetail>>();
                for (int i = 0; i < slotNames.Count; i++)
                    progressReporters.Add(multiScriptProgressService.CreateScriptProgress(i));

                var tasks = new List<Task<RemovalOutcome>>();
                int slotIndex = 0;

                foreach (var app in scriptApps)
                {
                    var progress = progressReporters[slotIndex];
                    var ct = cancellationToken;
                    var appName = app.Name;
                    tasks.Add(Task.Run(async () =>
                    {
                        var result = await bloatRemovalService.ExecuteDedicatedScriptAsync(app, progress, ct).ConfigureAwait(false);
                        progress.Report(new TaskProgressDetail { Progress = 100, StatusText = appName, IsCompletion = true });
                        return result;
                    }, ct));
                    slotIndex++;
                }

                if (regularApps.Count > 0)
                {
                    var progress = progressReporters[slotIndex];
                    var ct = cancellationToken;
                    tasks.Add(Task.Run(async () =>
                    {
                        var result = await bloatRemovalService.ExecuteBloatRemovalAsync(regularApps, progress, ct).ConfigureAwait(false);
                        progress.Report(new TaskProgressDetail { Progress = 100, StatusText = "Removing Apps", IsCompletion = true });
                        return result;
                    }, ct));
                }

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                if (saveRemovalScripts)
                {
                    logService.LogInformation("[UninstallAppsParallel] Persisting removal scripts...");
                    await bloatRemovalService.PersistRemovalScriptsAsync(apps).ConfigureAwait(false);
                }
                else
                {
                    logService.LogInformation("[UninstallAppsParallel] Cleaning up all removal artifacts...");
                    await bloatRemovalService.CleanupAllRemovalArtifactsAsync().ConfigureAwait(false);
                }

                var anyDeferred = results.Any(r => r == RemovalOutcome.DeferredToScheduledTask);
                if (anyDeferred)
                {
                    logService.LogWarning($"[UninstallAppsParallel] DONE: {apps.Count} apps deferred to scheduled task");
                    return OperationResult<int>.DeferredSuccess(apps.Count, "Items will be removed at next startup");
                }

                logService.Log(LogLevel.Success, $"[UninstallAppsParallel] DONE: Successfully removed {apps.Count} apps");
                return OperationResult<int>.Succeeded(apps.Count);
            }
            catch (OperationCanceledException)
            {
                logService.Log(LogLevel.Info, "Parallel removal was cancelled");
                return OperationResult<int>.Cancelled("Operation was cancelled");
            }
            catch (Exception ex)
            {
                logService.LogError($"Failed to remove apps in parallel: {ex.Message}");
                return OperationResult<int>.Failed(ex.Message);
            }
            finally
            {
                multiScriptProgressService.CompleteMultiScriptTask();
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to remove apps in parallel: {ex.Message}");
            return OperationResult<int>.Failed(ex.Message);
        }
    }

    private static string GetScriptSlotName(ItemDefinition app) => app.Id switch
    {
        "windows-app-edge" => "EdgeRemoval",
        "windows-app-onedrive" => "OneDriveRemoval",
        _ => app.Name
    };
}
