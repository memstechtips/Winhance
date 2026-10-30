using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class WindowsAppUninstallService(
    ILogService logService,
    IWindowsAppsService windowsAppsService,
    IBloatRemovalService bloatRemovalService,
    ITaskProgressService taskProgressService,
    IMultiScriptProgressService multiScriptProgressService,
    IChangeHistoryService changeHistory,
    ISettingApplicationService settingApplicationService,
    IPendingRestartService pendingRestartService) : IWindowsAppUninstallService
{
    public async Task<OperationResult<bool>> UninstallAppAsync(string appId, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = taskProgressService.GetCurrentCancellationToken();

        try
        {
            logService.LogInformation($"START: appId='{appId}'");
            var app = await windowsAppsService.GetAppByIdAsync(appId).ConfigureAwait(false);
            if (app == null)
            {
                logService.LogInformation($"App '{appId}' not found in definitions");
                return OperationResult<bool>.Failed("App not found");
            }

            logService.LogInformation($"Found: '{app.Name}' AppX={( app.AppxPackageName != null ? string.Join(", ", app.AppxPackageName) : "null")} Cap={app.CapabilityName ?? "null"} Feat={app.OptionalFeatureName ?? "null"}");

            RemovalOutcome outcome;

            if (app.RemovalScript != null)
            {
                logService.LogInformation($"Routing to ExecuteDedicatedScriptAsync for '{app.Name}'");
                outcome = await bloatRemovalService.ExecuteDedicatedScriptAsync(app, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                logService.LogInformation($"Routing to ExecuteBloatRemovalAsync for '{app.Name}'");
                outcome = await bloatRemovalService.ExecuteBloatRemovalAsync([app], progress, cancellationToken).ConfigureAwait(false);
            }

            if (outcome is RemovalOutcome.Success or RemovalOutcome.DeferredToScheduledTask)
            {
                changeHistory.LogAppChange(app.Name, AppChangeKind.Removed);
                await TurnOffGameDvrIfGameBarRemovedAsync([app]).ConfigureAwait(false);
            }
            if (outcome == RemovalOutcome.Success)
                RaiseExplorerRestartIfNeeded([app]);

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
        var cancellationToken = taskProgressService.GetCurrentCancellationToken();

        try
        {
            if (apps == null || apps.Count == 0)
                return OperationResult<int>.Failed("No apps provided");

            logService.LogInformation($"START: {apps.Count} total apps to process");

            var scriptApps = DedicatedRemovalScripts.OnePerScript(apps);
            var regularApps = apps.Where(a => a.RemovalScript == null).ToList();

            logService.LogInformation($"Categorization: ScriptApps={scriptApps.Count}, RegularApps={regularApps.Count}");
            foreach (var a in apps)
                logService.LogInformation($"  Item: '{a.Name}' Id={a.Id} AppX={(a.AppxPackageName != null ? string.Join(", ", a.AppxPackageName) : "null")} Cap={a.CapabilityName ?? "null"} Feat={a.OptionalFeatureName ?? "null"} IsInstalled={a.IsInstalled}");

            var anyDeferred = false;

            if (scriptApps.Count > 0)
            {
                logService.LogInformation($"Step 2: Executing {scriptApps.Count} dedicated scripts...");
                foreach (var app in scriptApps)
                {
                    var outcome = await bloatRemovalService.ExecuteDedicatedScriptAsync(app, progress, cancellationToken).ConfigureAwait(false);
                    var removed = RemovedByScript(app, apps);
                    if (outcome is RemovalOutcome.Success or RemovalOutcome.DeferredToScheduledTask)
                    {
                        foreach (var item in removed)
                            changeHistory.LogAppChange(item.Name, AppChangeKind.Removed);
                    }
                    if (outcome == RemovalOutcome.Success)
                        RaiseExplorerRestartIfNeeded(removed);
                    if (outcome == RemovalOutcome.DeferredToScheduledTask) anyDeferred = true;
                }
                logService.LogInformation("Step 2 DONE");
            }

            if (regularApps.Count > 0)
            {
                logService.LogInformation($"Step 3: Executing BloatRemoval for {regularApps.Count} regular apps...");
                var regularOutcome = await bloatRemovalService.ExecuteBloatRemovalAsync(regularApps, progress, cancellationToken).ConfigureAwait(false);
                if (regularOutcome is RemovalOutcome.Success or RemovalOutcome.DeferredToScheduledTask)
                {
                    foreach (var app in regularApps)
                        changeHistory.LogAppChange(app.Name, AppChangeKind.Removed);
                    await TurnOffGameDvrIfGameBarRemovedAsync(regularApps).ConfigureAwait(false);
                }
                if (regularOutcome == RemovalOutcome.Success)
                    RaiseExplorerRestartIfNeeded(regularApps);
                if (regularOutcome == RemovalOutcome.DeferredToScheduledTask) anyDeferred = true;
                logService.LogInformation("Step 3 DONE");
            }

            if (saveRemovalScripts)
            {
                logService.LogInformation("Step 4: Persisting removal scripts...");
                await bloatRemovalService.PersistRemovalScriptsAsync(apps).ConfigureAwait(false);
            }
            else
            {
                logService.LogInformation("Step 4: Cleaning up all removal artifacts...");
                await bloatRemovalService.CleanupAllRemovalArtifactsAsync().ConfigureAwait(false);
            }
            logService.LogInformation("Step 4 DONE");

            if (anyDeferred)
            {
                logService.LogWarning($"DONE: {apps.Count} apps deferred to scheduled task");
                return OperationResult<int>.DeferredSuccess(apps.Count, "Items will be removed at next startup");
            }

            logService.Log(LogLevel.Success, $"DONE: Successfully removed {apps.Count} apps");
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
            if (apps == null || apps.Count == 0)
                return OperationResult<int>.Failed("No apps provided");

            logService.LogInformation($"START: {apps.Count} total apps to process");

            var scriptApps = DedicatedRemovalScripts.OnePerScript(apps);
            var regularApps = apps.Where(a => a.RemovalScript == null).ToList();

            logService.LogInformation($"ScriptApps={scriptApps.Count}, RegularApps={regularApps.Count}");

            var slotNames = new List<string>();
            foreach (var app in scriptApps)
                slotNames.Add(app.RemovalScriptName!);
            if (regularApps.Count > 0)
                slotNames.Add(BloatRemovalScriptGenerator.ScriptName);

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

                // Log history: first scriptApps.Count results are per-dedicated-script outcomes;
                // the last result (if regularApps.Count > 0) is the batch outcome for regular apps.
                for (int i = 0; i < scriptApps.Count; i++)
                {
                    var removed = RemovedByScript(scriptApps[i], apps);
                    if (results[i] is RemovalOutcome.Success or RemovalOutcome.DeferredToScheduledTask)
                    {
                        foreach (var item in removed)
                            changeHistory.LogAppChange(item.Name, AppChangeKind.Removed);
                    }
                    if (results[i] == RemovalOutcome.Success)
                        RaiseExplorerRestartIfNeeded(removed);
                }
                if (regularApps.Count > 0)
                {
                    var regularOutcome = results[scriptApps.Count];
                    if (regularOutcome is RemovalOutcome.Success or RemovalOutcome.DeferredToScheduledTask)
                    {
                        foreach (var app in regularApps)
                            changeHistory.LogAppChange(app.Name, AppChangeKind.Removed);
                        await TurnOffGameDvrIfGameBarRemovedAsync(regularApps).ConfigureAwait(false);
                    }
                    if (regularOutcome == RemovalOutcome.Success)
                        RaiseExplorerRestartIfNeeded(regularApps);
                }

                if (saveRemovalScripts)
                {
                    logService.LogInformation("Persisting removal scripts...");
                    await bloatRemovalService.PersistRemovalScriptsAsync(apps).ConfigureAwait(false);
                }
                else
                {
                    logService.LogInformation("Cleaning up all removal artifacts...");
                    await bloatRemovalService.CleanupAllRemovalArtifactsAsync().ConfigureAwait(false);
                }

                var anyDeferred = results.Any(r => r == RemovalOutcome.DeferredToScheduledTask);
                if (anyDeferred)
                {
                    logService.LogWarning($"DONE: {apps.Count} apps deferred to scheduled task");
                    return OperationResult<int>.DeferredSuccess(apps.Count, "Items will be removed at next startup");
                }

                logService.Log(LogLevel.Success, $"DONE: Successfully removed {apps.Count} apps");
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

    // A failure here is logged, not returned: the apps are already gone, and the Game DVR card still offers the switch.
    private async Task TurnOffGameDvrIfGameBarRemovedAsync(IEnumerable<ItemDefinition> removed)
    {
        if (!GameBarRemoval.Includes(removed.SelectMany(app => app.AppxPackageName ?? [])))
            return;

        logService.LogInformation("Game Bar was removed, turning Game DVR off");
        try
        {
            var result = await settingApplicationService.ApplySettingAsync(new ApplySettingRequest
            {
                SettingId = GameBarRemoval.GameDvrSettingId,
                Enable = false,
            }).ConfigureAwait(false);
            if (!result.Success)
                logService.LogWarning($"Could not turn Game DVR off after removing Game Bar: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not turn Game DVR off after removing Game Bar: {ex.Message}");
        }
    }

    // A deferred removal has not happened yet, so only a finished one raises the bar.
    private void RaiseExplorerRestartIfNeeded(IEnumerable<ItemDefinition> removed)
    {
        foreach (var app in removed.Where(a => a.RequiresExplorerRestart))
            pendingRestartService.Register(app.Id);
    }

    private static List<ItemDefinition> RemovedByScript(ItemDefinition scriptApp, List<ItemDefinition> apps) =>
        [scriptApp, .. apps.Where(a => scriptApp.AlsoRemoves?.Contains(a.Id) == true)];
}
