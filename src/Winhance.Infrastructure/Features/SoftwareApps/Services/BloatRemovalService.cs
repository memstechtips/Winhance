using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class BloatRemovalService(
    ILogService logService,
    IScheduledTaskService scheduledTaskService,
    IPowerShellRunner powerShellRunner) : IBloatRemovalService
{
    public async Task<RemovalOutcome> ExecuteDedicatedScriptAsync(
        ItemDefinition app,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var scriptName = CreateScriptName(app.Id);
            var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, scriptName);
            var scriptContent = app.RemovalScript!();

            Directory.CreateDirectory(ScriptPaths.ScriptsDirectory);
            await File.WriteAllTextAsync(scriptPath, scriptContent, ct).ConfigureAwait(false);

            // Emit metadata header for the task output dialog
            var startTime = DateTime.Now;
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"Command: powershell.exe -ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\""
            });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"Start Time: \"{startTime:yyyy/MM/dd HH:mm:ss}\""
            });
            progress?.Report(new TaskProgressDetail { TerminalOutput = "---" });

            logService.LogInformation($"Executing dedicated removal script for '{app.Name}' from {scriptPath}...");
            await powerShellRunner.RunScriptFileAsync(scriptPath, progress: progress, ct: ct).ConfigureAwait(false);
            logService.LogInformation($"Dedicated removal script for '{app.Name}' completed successfully");

            // Emit metadata footer
            var endTime = DateTime.Now;
            progress?.Report(new TaskProgressDetail { TerminalOutput = "---" });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"End Time: \"{endTime:yyyy/MM/dd HH:mm:ss}\""
            });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = "Process return value: \"0\" (0x00000000)"
            });

            return RemovalOutcome.Success;
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation($"Dedicated script execution for '{app.Name}' was cancelled");
            return RemovalOutcome.Failed;
        }
        catch (ExecutionPolicyException ex)
        {
            // Emit metadata footer indicating deferral
            var endTime = DateTime.Now;
            progress?.Report(new TaskProgressDetail { TerminalOutput = "---" });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"End Time: \"{endTime:yyyy/MM/dd HH:mm:ss}\""
            });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = "Execution policy blocked script — removal deferred to scheduled task"
            });

            logService.LogWarning($"Execution policy blocked dedicated script for '{app.Name}', deferring to scheduled task: {ex.Message}");
            return RemovalOutcome.DeferredToScheduledTask;
        }
        catch (InvalidOperationException ex)
        {
            // Emit metadata footer with non-zero exit code
            var endTime = DateTime.Now;
            progress?.Report(new TaskProgressDetail { TerminalOutput = "---" });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"End Time: \"{endTime:yyyy/MM/dd HH:mm:ss}\""
            });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"Process return value: \"{ex.Message}\""
            });

            logService.LogWarning($"Dedicated script for '{app.Name}' completed with warnings: {ex.Message}");
            return RemovalOutcome.Success;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error executing dedicated script for '{app.Name}': {ex.Message}", ex);
            return RemovalOutcome.Failed;
        }
    }

    public async Task<RemovalOutcome> ExecuteBloatRemovalAsync(
        List<ItemDefinition> apps,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var (packages, capabilities, optionalFeatures, specialApps) = CategorizeApps(apps);

            bool hasItems = packages.Any() || capabilities.Any() || optionalFeatures.Any() || specialApps.Any();
            if (!hasItems)
            {
                logService.LogInformation("No items to process in BloatRemoval");
                return RemovalOutcome.Success;
            }

            Directory.CreateDirectory(ScriptPaths.ScriptsDirectory);
            var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");

            string scriptContent;
            if (File.Exists(scriptPath))
            {
                scriptContent = await MergeWithExistingScript(scriptPath, packages, capabilities, optionalFeatures, specialApps).ConfigureAwait(false);
            }
            else
            {
                scriptContent = GenerateScriptContent(packages, capabilities, optionalFeatures, specialApps);
            }

            await File.WriteAllTextAsync(scriptPath, scriptContent, ct).ConfigureAwait(false);

            // Emit metadata header for the task output dialog
            var startTime = DateTime.Now;
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"Command: powershell.exe -ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\""
            });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"Start Time: \"{startTime:yyyy/MM/dd HH:mm:ss}\""
            });
            progress?.Report(new TaskProgressDetail { TerminalOutput = "---" });

            logService.LogInformation($"Executing BloatRemoval script from {scriptPath} ({packages.Count} packages, {capabilities.Count} capabilities, {optionalFeatures.Count} features, {specialApps.Count} special)...");
            await powerShellRunner.RunScriptFileAsync(scriptPath, progress: progress, ct: ct).ConfigureAwait(false);
            logService.LogInformation("BloatRemoval script completed successfully");

            // Emit metadata footer
            var endTime = DateTime.Now;
            progress?.Report(new TaskProgressDetail { TerminalOutput = "---" });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"End Time: \"{endTime:yyyy/MM/dd HH:mm:ss}\""
            });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = "Process return value: \"0\" (0x00000000)"
            });

            return RemovalOutcome.Success;
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation("BloatRemoval script execution was cancelled");
            return RemovalOutcome.Failed;
        }
        catch (ExecutionPolicyException ex)
        {
            // Emit metadata footer indicating deferral
            var endTime = DateTime.Now;
            progress?.Report(new TaskProgressDetail { TerminalOutput = "---" });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"End Time: \"{endTime:yyyy/MM/dd HH:mm:ss}\""
            });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = "Execution policy blocked script — removal deferred to scheduled task"
            });

            logService.LogWarning($"Execution policy blocked BloatRemoval script, deferring to scheduled task: {ex.Message}");
            return RemovalOutcome.DeferredToScheduledTask;
        }
        catch (InvalidOperationException ex)
        {
            // Emit metadata footer with non-zero exit code
            var endTime = DateTime.Now;
            progress?.Report(new TaskProgressDetail { TerminalOutput = "---" });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"End Time: \"{endTime:yyyy/MM/dd HH:mm:ss}\""
            });
            progress?.Report(new TaskProgressDetail
            {
                TerminalOutput = $"Process return value: \"{ex.Message}\""
            });

            logService.LogWarning($"BloatRemoval script completed with warnings: {ex.Message}");
            return RemovalOutcome.Success;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error executing BloatRemoval script: {ex.Message}", ex);
            return RemovalOutcome.Failed;
        }
    }

    public async Task PersistRemovalScriptsAsync(List<ItemDefinition> allApps)
    {
        // Scripts are already on disk from ExecuteDedicatedScriptAsync / ExecuteBloatRemovalAsync.
        // Here we just register the scheduled tasks so they run on startup/login.

        // Register dedicated script tasks (Edge, OneDrive)
        var dedicatedApps = allApps.Where(a => a.RemovalScript != null).ToList();
        foreach (var app in dedicatedApps)
        {
            var scriptName = CreateScriptName(app.Id);
            var taskName = scriptName.Replace(".ps1", "");
            var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, scriptName);

            if (!File.Exists(scriptPath))
            {
                logService.LogWarning($"Script not found for task registration: {scriptPath}");
                continue;
            }

            var scriptContent = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);
            var runOnStartup = scriptName.Equals("EdgeRemoval.ps1", StringComparison.OrdinalIgnoreCase);
            var script = new RemovalScript
            {
                Name = taskName,
                Content = scriptContent,
                TargetScheduledTaskName = taskName,
                RunOnStartup = runOnStartup,
                ActualScriptPath = scriptPath
            };
            await scheduledTaskService.RegisterScheduledTaskAsync(script).ConfigureAwait(false);
            logService.LogInformation($"Registered scheduled task for: {taskName}");
        }

        // Register BloatRemoval task
        var bloatScriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");
        if (File.Exists(bloatScriptPath))
        {
            var bloatContent = await File.ReadAllTextAsync(bloatScriptPath).ConfigureAwait(false);
            var bloatScript = new RemovalScript
            {
                Name = "BloatRemoval",
                Content = bloatContent,
                TargetScheduledTaskName = "BloatRemoval",
                RunOnStartup = false,
                ActualScriptPath = bloatScriptPath
            };
            await scheduledTaskService.RegisterScheduledTaskAsync(bloatScript).ConfigureAwait(false);
            logService.LogInformation("Registered scheduled task for: BloatRemoval");
        }
    }

    public async Task CleanupAllRemovalArtifactsAsync()
    {
        // Clean up EdgeRemoval
        var edgePath = Path.Combine(ScriptPaths.ScriptsDirectory, "EdgeRemoval.ps1");
        await CleanupExistingScheduledTaskAsync("EdgeRemoval", edgePath).ConfigureAwait(false);

        // Clean up OneDriveRemoval
        var oneDrivePath = Path.Combine(ScriptPaths.ScriptsDirectory, "OneDriveRemoval.ps1");
        await CleanupExistingScheduledTaskAsync("OneDriveRemoval", oneDrivePath).ConfigureAwait(false);

        // Clean up BloatRemoval
        await CleanupBloatRemovalArtifactsAsync().ConfigureAwait(false);
    }

    public async Task<bool> RemoveItemsFromScriptAsync(List<ItemDefinition> itemsToRemove)
    {
        try
        {
            var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");

            if (!File.Exists(scriptPath))
            {
                logService.LogInformation("BloatRemoval.ps1 does not exist, nothing to clean up.");
                return true;
            }

            var existingContent = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);
            var itemsToRemoveNames = GetItemNames(itemsToRemove);

            var updatedContent = RemoveItemsFromScriptContent(existingContent, itemsToRemoveNames);

            if (updatedContent != existingContent)
            {
                logService.LogInformation($"Removed {itemsToRemoveNames.Count} items from BloatRemoval.ps1");

                if (IsScriptEmpty(updatedContent))
                {
                    logService.LogInformation("BloatRemoval script has no remaining items, cleaning up artifacts");
                    await CleanupBloatRemovalArtifactsAsync().ConfigureAwait(false);
                }
                else
                {
                    await File.WriteAllTextAsync(scriptPath, updatedContent).ConfigureAwait(false);

                    // Re-register the scheduled task with updated content
                    var script = new RemovalScript
                    {
                        Name = "BloatRemoval",
                        Content = updatedContent,
                        TargetScheduledTaskName = "BloatRemoval",
                        RunOnStartup = false,
                        ActualScriptPath = scriptPath
                    };
                    await scheduledTaskService.RegisterScheduledTaskAsync(script).ConfigureAwait(false);
                }

                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error removing items from script: {ex.Message}", ex);
            return false;
        }
    }

    private async Task CleanupBloatRemovalArtifactsAsync()
    {
        var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");
        await CleanupExistingScheduledTaskAsync("BloatRemoval", scriptPath).ConfigureAwait(false);
    }

    // --- Private helpers ---

    private static (List<string> packages, List<string> capabilities, List<string> optionalFeatures, List<string> specialApps)
        CategorizeApps(List<ItemDefinition> apps)
    {
        var packages = new List<string>();
        var capabilities = new List<string>();
        var optionalFeatures = new List<string>();
        var specialApps = new List<string>();

        foreach (var app in apps.Where(a => a.RemovalScript == null))
        {
            var name = GetAppName(app);
            if (string.IsNullOrEmpty(name)) continue;

            if (!string.IsNullOrEmpty(app.CapabilityName))
                capabilities.Add(name);
            else if (!string.IsNullOrEmpty(app.OptionalFeatureName))
                optionalFeatures.Add(name);
            else
            {
                packages.Add(name);
                if (app.SubPackages?.Any() == true)
                    packages.AddRange(app.SubPackages);
                if (IsOneNote(app))
                    specialApps.Add("OneNote");
            }
        }

        return (packages, capabilities, optionalFeatures, specialApps);
    }

    private async Task CleanupExistingScheduledTaskAsync(string taskName, string scriptPath)
    {
        try
        {
            if (await scheduledTaskService.IsTaskRegisteredAsync(taskName).ConfigureAwait(false))
            {
                await scheduledTaskService.UnregisterScheduledTaskAsync(taskName).ConfigureAwait(false);
                logService.LogInformation($"Unregistered existing scheduled task: {taskName}");
            }

            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
                logService.LogInformation($"Deleted existing script: {scriptPath}");
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Error cleaning up existing script/task '{taskName}': {ex.Message}");
        }
    }

    private async Task<string> MergeWithExistingScript(string scriptPath, List<string> packages, List<string> capabilities, List<string> optionalFeatures, List<string> specialApps)
    {
        var existingContent = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);

        var existingPackages = ExtractArrayFromScript(existingContent, "packages");
        var existingCapabilities = ExtractArrayFromScript(existingContent, "capabilities");
        var existingFeatures = ExtractArrayFromScript(existingContent, "optionalFeatures");
        var existingSpecialApps = ExtractArrayFromScript(existingContent, "specialApps");

        var mergedPackages = existingPackages.Union(packages).Distinct().ToList();
        var mergedCapabilities = existingCapabilities.Union(capabilities).Distinct().ToList();
        var mergedFeatures = existingFeatures.Union(optionalFeatures).Distinct().ToList();
        var mergedSpecialApps = existingSpecialApps.Union(specialApps).Distinct().ToList();

        return GenerateScriptContent(mergedPackages, mergedCapabilities, mergedFeatures, mergedSpecialApps);
    }

    private string CreateScriptName(string appId)
    {
        return appId switch
        {
            "windows-app-edge" => "EdgeRemoval.ps1",
            "windows-app-onedrive" => "OneDriveRemoval.ps1",
            _ => throw new NotSupportedException($"No dedicated script defined for {appId}")
        };
    }

    private List<string> GetItemNames(List<ItemDefinition> items)
    {
        var names = new List<string>();
        foreach (var item in items)
        {
            var name = GetAppName(item);
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }
        return names;
    }

    private bool IsScriptEmpty(string content)
    {
        return !ExtractArrayFromScript(content, "packages").Any()
            && !ExtractArrayFromScript(content, "capabilities").Any()
            && !ExtractArrayFromScript(content, "optionalFeatures").Any()
            && !ExtractArrayFromScript(content, "specialApps").Any();
    }

    private string RemoveItemsFromScriptContent(string content, List<string> itemsToRemove)
    {
        var existingPackages = ExtractArrayFromScript(content, "packages");
        var existingCapabilities = ExtractArrayFromScript(content, "capabilities");
        var existingFeatures = ExtractArrayFromScript(content, "optionalFeatures");
        var existingSpecialApps = ExtractArrayFromScript(content, "specialApps");

        var cleanedPackages = existingPackages.Except(itemsToRemove, StringComparer.OrdinalIgnoreCase).ToList();
        var cleanedCapabilities = existingCapabilities.Except(itemsToRemove, StringComparer.OrdinalIgnoreCase).ToList();
        var cleanedFeatures = existingFeatures.Except(itemsToRemove, StringComparer.OrdinalIgnoreCase).ToList();
        var cleanedSpecialApps = existingSpecialApps.Where(specialApp =>
        {
            if (itemsToRemove.Any(item => specialApp.Equals(item, StringComparison.OrdinalIgnoreCase)))
                return false;

            return !itemsToRemove.Any(item => IsOneNotePackage(item, specialApp));
        }).ToList();

        return GenerateScriptContent(cleanedPackages, cleanedCapabilities, cleanedFeatures, cleanedSpecialApps);
    }

    private static List<string> ExtractArrayFromScript(string content, string arrayName)
        => BloatRemovalScriptGenerator.ExtractArrayFromScript(content, arrayName);

    private static string GetAppName(ItemDefinition app)
    {
        if (!string.IsNullOrEmpty(app.CapabilityName))
            return app.CapabilityName;

        if (!string.IsNullOrEmpty(app.OptionalFeatureName))
            return app.OptionalFeatureName;

        return app.AppxPackageName!;
    }

    private string GenerateScriptContent(List<string> packages, List<string> capabilities, List<string> features, List<string>? specialApps = null)
    {
        var xboxPackages = new[] { "Microsoft.GamingApp", "Microsoft.XboxGamingOverlay", "Microsoft.XboxGameOverlay" };
        var includeXboxFix = packages.Any(p => xboxPackages.Contains(p, StringComparer.OrdinalIgnoreCase));
        var includeTeamsKill = packages.Any(p => p.Equals("MSTeams", StringComparison.OrdinalIgnoreCase));

        return BloatRemovalScriptGenerator.GenerateScript(
            packages,
            capabilities,
            features,
            specialApps ?? new List<string>(),
            includeXboxFix,
            includeTeamsKill);
    }

    private static bool IsOneNote(ItemDefinition app)
    {
        return app.AppxPackageName?.Contains("OneNote", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsOneNotePackage(string packageName, string specialAppType)
    {
        return specialAppType.Equals("OneNote", StringComparison.OrdinalIgnoreCase) &&
               packageName.Contains("OneNote", StringComparison.OrdinalIgnoreCase);
    }
}
