using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class BloatRemovalService(
    ILogService logService,
    IScheduledTaskService scheduledTaskService,
    IPowerShellExecutionService powerShellService) : IBloatRemovalService
{
    public async Task<bool> RemoveAppsAsync(
        List<ItemDefinition> selectedApps,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default,
        bool saveRemovalScripts = true)
    {
        try
        {
            var scriptPath = await CreateOrUpdateBloatRemovalScript(selectedApps, progress, cancellationToken, saveRemovalScripts);

            if (!string.IsNullOrEmpty(scriptPath))
            {
                var success = await ExecuteRemovalScriptAsync(scriptPath, progress, cancellationToken);

                if (saveRemovalScripts)
                {
                    await RegisterStartupTaskAsync(scriptPath);
                }
                else
                {
                    // Delete the script after execution — no trace left on disk
                    try { File.Delete(scriptPath); }
                    catch (Exception ex) { logService.LogError($"Failed to delete temporary script: {ex.Message}"); }

                    // Also remove any pre-existing scheduled task for BloatRemoval
                    // Task is registered under script.Name ("BloatRemoval"), not TargetScheduledTaskName
                    await CleanupExistingScheduledTaskAsync("BloatRemoval", scriptPath);
                }

                return success;
            }
            else
            {
                logService.LogInformation("App removal completed using dedicated scripts only");
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation("App removal was cancelled by user");
            return false;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error removing apps: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> RemoveSpecialAppsAsync(
        List<string> specialAppTypes,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var supportedApps = specialAppTypes.Where(app =>
                app.Equals("OneNote", StringComparison.OrdinalIgnoreCase)).ToList();

            if (!supportedApps.Any())
                return true;

            var scriptPath = await CreateSpecialAppRemovalScript(supportedApps);
            var success = await ExecuteRemovalScriptAsync(scriptPath, progress, cancellationToken);
            await RegisterStartupTaskAsync(scriptPath);
            return success;
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation("Special app removal was cancelled by user");
            return false;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error removing special apps: {ex.Message}", ex);
            return false;
        }
    }

    private async Task<string> CreateSpecialAppRemovalScript(List<string> specialApps)
    {
        Directory.CreateDirectory(ScriptPaths.ScriptsDirectory);
        var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");

        var scriptContent = GenerateScriptContent(
            packages: new List<string>(),
            capabilities: new List<string>(),
            features: new List<string>(),
            specialApps: specialApps);

        await File.WriteAllTextAsync(scriptPath, scriptContent);
        logService.LogInformation($"Special app removal script created at: {scriptPath}");
        return scriptPath;
    }

    public async Task<bool> ExecuteRemovalScriptAsync(string scriptPath, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logService.LogInformation($"Executing removal script: {scriptPath}");

            await powerShellService.ExecuteScriptFileWithProgressAsync(scriptPath, "", progress, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation("Script execution was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error executing script: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> RegisterStartupTaskAsync(string scriptPath)
    {
        try
        {
            var script = new RemovalScript
            {
                Name = "BloatRemoval",
                Content = await File.ReadAllTextAsync(scriptPath),
                TargetScheduledTaskName = "WinhanceBloatRemoval",
                RunOnStartup = false,
                ActualScriptPath = scriptPath
            };

            return await scheduledTaskService.RegisterScheduledTaskAsync(script);
        }
        catch (Exception ex)
        {
            logService.LogError($"Error registering scheduled task: {ex.Message}", ex);
            return false;
        }
    }

    private async Task CleanupExistingScheduledTaskAsync(string taskName, string scriptPath)
    {
        try
        {
            if (await scheduledTaskService.IsTaskRegisteredAsync(taskName))
            {
                await scheduledTaskService.UnregisterScheduledTaskAsync(taskName);
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

    private async Task<string> CreateOrUpdateBloatRemovalScript(
        List<ItemDefinition> apps,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default,
        bool saveRemovalScripts = true)
    {
        Directory.CreateDirectory(ScriptPaths.ScriptsDirectory);
        var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");

        var packages = new List<string>();
        var capabilities = new List<string>();
        var optionalFeatures = new List<string>();
        var specialApps = new List<string>();

        // Handle apps with dedicated scripts first
        var appsWithScripts = apps.Where(a => a.RemovalScript != null).ToList();
        for (int i = 0; i < appsWithScripts.Count; i++)
        {
            await CreateDedicatedRemovalScript(appsWithScripts[i], i, appsWithScripts.Count, progress, cancellationToken, saveRemovalScripts);
        }

        // Handle regular apps (including OneNote)
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
                {
                    packages.AddRange(app.SubPackages);
                }

                if (IsOneNote(app))
                    specialApps.Add("OneNote");
            }
        }

        bool hasRegularApps = packages.Any() || capabilities.Any() || optionalFeatures.Any() || specialApps.Any();

        if (!hasRegularApps)
        {
            logService.LogInformation("No regular apps to process. Skipping BloatRemoval.ps1 creation.");
            return string.Empty;
        }

        string scriptContent;
        if (File.Exists(scriptPath))
        {
            scriptContent = await MergeWithExistingScript(scriptPath, packages, capabilities, optionalFeatures, specialApps);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            scriptContent = GenerateScriptContent(packages, capabilities, optionalFeatures, specialApps);
        }

        await File.WriteAllTextAsync(scriptPath, scriptContent);
        logService.LogInformation($"Script updated at: {scriptPath}");
        return scriptPath;
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

    private async Task CreateDedicatedRemovalScript(
        ItemDefinition app,
        int currentIndex,
        int totalCount,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default,
        bool saveRemovalScripts = true)
    {
        var scriptName = CreateScriptName(app.Id);
        var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, scriptName);
        var scriptContent = app.RemovalScript!();

        int baseProgress = 10 + (currentIndex * 80 / totalCount);
        int scriptProgressRange = 80 / totalCount;

        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, scriptContent);
        logService.LogInformation($"Dedicated removal script created at: {scriptPath}");

        var executionSuccess = await ExecuteRemovalScriptAsync(scriptPath, progress, cancellationToken);
        logService.LogInformation($"Script execution result: {executionSuccess}");

        if (saveRemovalScripts)
        {
            var runOnStartup = scriptName.Equals("EdgeRemoval.ps1", StringComparison.OrdinalIgnoreCase);

            var script = new RemovalScript
            {
                Name = scriptName.Replace(".ps1", ""),
                Content = scriptContent,
                TargetScheduledTaskName = scriptName.Replace(".ps1", ""),
                RunOnStartup = runOnStartup,
                ActualScriptPath = scriptPath
            };

            await scheduledTaskService.RegisterScheduledTaskAsync(script);
        }
        else
        {
            var taskName = scriptName.Replace(".ps1", "");

            // Delete the dedicated script after execution — no trace left on disk
            try { File.Delete(scriptPath); }
            catch (Exception ex) { logService.LogError($"Failed to delete temporary dedicated script: {ex.Message}"); }

            // Also remove any pre-existing scheduled task for this dedicated script
            await CleanupExistingScheduledTaskAsync(taskName, scriptPath);
        }
    }

    private async Task<string> MergeWithExistingScript(string scriptPath, List<string> packages, List<string> capabilities, List<string> optionalFeatures, List<string> specialApps)
    {
        var existingContent = await File.ReadAllTextAsync(scriptPath);

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

            var existingContent = await File.ReadAllTextAsync(scriptPath);
            var itemsToRemoveNames = GetItemNames(itemsToRemove);

            var updatedContent = RemoveItemsFromScriptContent(existingContent, itemsToRemoveNames);

            if (updatedContent != existingContent)
            {
                await File.WriteAllTextAsync(scriptPath, updatedContent);
                logService.LogInformation($"Removed {itemsToRemoveNames.Count} items from BloatRemoval.ps1");

                await RegisterStartupTaskAsync(scriptPath);

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

    private List<string> ExtractArrayFromScript(string content, string arrayName)
    {
        var pattern = $@"\${arrayName}\s*=\s*@\(\s*(.*?)\s*\)";
        var match = Regex.Match(content, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (!match.Success) return new List<string>();

        var arrayContent = match.Groups[1].Value;
        var items = arrayContent
            .Split('\n')
            .Select(line => line.Trim().Trim(',').Trim('\'', '"'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        return items;
    }

    private string GetAppName(ItemDefinition app)
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

        return BloatRemovalScriptGenerator.GenerateScript(
            packages,
            capabilities,
            features,
            specialApps ?? new List<string>(),
            includeXboxFix);
    }


    private bool IsOneNote(ItemDefinition app)
    {
        return app.AppxPackageName?.Contains("OneNote", StringComparison.OrdinalIgnoreCase) == true;
    }

    private bool IsOneNotePackage(string packageName, string specialAppType)
    {
        return specialAppType.Equals("OneNote", StringComparison.OrdinalIgnoreCase) &&
               packageName.Contains("OneNote", StringComparison.OrdinalIgnoreCase);
    }
}