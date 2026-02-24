using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ScheduledTaskService(ILogService logService, IFileSystemService fileSystemService) : IScheduledTaskService
{
    private enum TaskTriggerType
    {
        Startup = 8,
        Logon = 9
    }

    public async Task<OperationResult> RegisterScheduledTaskAsync(RemovalScript script)
    {
        return await Task.Run(async () =>
        {
            try
            {
                if (script?.ActualScriptPath == null)
                {
                    logService.LogError("Script or script path is null");
                    return OperationResult.Failed("Script or script path is null");
                }

                EnsureScriptFileExists(script);

                var triggerType = script.RunOnStartup ? TaskTriggerType.Startup : TaskTriggerType.Logon;

                return await RegisterTaskInternal(script.Name, script.ActualScriptPath, null, triggerType).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logService.LogError($"Error registering scheduled task for {script?.Name}", ex);
                return OperationResult.Failed(ex.Message, ex);
            }
        }).ConfigureAwait(false);
    }


    public async Task<OperationResult> UnregisterScheduledTaskAsync(string taskName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var taskService = CreateTaskService();
                var folder = GetWinhanceFolder(taskService);

                if (folder == null) return OperationResult.Succeeded();

                try
                {
                    var existingTask = folder.GetTask(taskName);
                    if (existingTask != null)
                    {
                        folder.DeleteTask(taskName, 0);
                        logService.LogInformation($"Unregistered task: {taskName}");
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"[ScheduledTaskService] Task '{taskName}' not found: {ex.Message}");
                }

                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                logService.LogError($"Error unregistering task: {taskName}", ex);
                return OperationResult.Failed(ex.Message, ex);
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> IsTaskRegisteredAsync(string taskName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var taskService = CreateTaskService();
                var folder = GetWinhanceFolder(taskService);

                if (folder == null) return false;

                var task = folder.GetTask(taskName);
                return task != null;
            }
            catch (Exception ex)
            {
                logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"[ScheduledTaskService] Task '{taskName}' not registered: {ex.Message}");
                return false;
            }
        }).ConfigureAwait(false);
    }

    public async Task<OperationResult> RunScheduledTaskAsync(string taskName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var taskService = CreateTaskService();
                var folder = GetWinhanceFolder(taskService);

                if (folder == null)
                {
                    logService.LogError($"Winhance task folder not found when trying to run: {taskName}");
                    return OperationResult.Failed("Winhance task folder not found");
                }

                var task = folder.GetTask(taskName);
                if (task == null)
                {
                    logService.LogError($"Task not found: {taskName}");
                    return OperationResult.Failed($"Task not found: {taskName}");
                }

                task.Run(null);
                logService.LogInformation($"Started task: {taskName}");
                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                logService.LogError($"Error running task: {taskName}", ex);
                return OperationResult.Failed(ex.Message, ex);
            }
        }).ConfigureAwait(false);
    }

    public async Task<OperationResult> CreateUserLogonTaskAsync(string taskName, string command, string username, bool deleteAfterRun = true)
    {
        return await Task.Run(async () =>
        {
            try
            {
                return await RegisterTaskInternal(taskName, null, username, TaskTriggerType.Logon, command).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logService.LogError($"Error creating user logon task: {taskName}", ex);
                return OperationResult.Failed(ex.Message, ex);
            }
        }).ConfigureAwait(false);
    }

    private async Task<OperationResult> RegisterTaskInternal(string taskName, string? scriptPath, string? username, TaskTriggerType triggerType, string? command = null)
    {
        var taskService = CreateTaskService();
        var folder = GetOrCreateWinhanceFolder(taskService);

        await RemoveExistingTask(folder, taskName).ConfigureAwait(false);

        var taskDefinition = CreateTaskDefinition(taskService, scriptPath, command, username, triggerType);

        folder.RegisterTaskDefinition(
            taskName,
            taskDefinition,
            6, // TASK_CREATE_OR_UPDATE
            username,
            null, // password
            username != null ? 1 : 5, // TASK_LOGON_INTERACTIVE_TOKEN or TASK_LOGON_SERVICE_ACCOUNT
            null
        );

        logService.LogInformation($"Registered task: {taskName} as {username ?? "SYSTEM"}");
        return OperationResult.Succeeded();
    }

    private dynamic CreateTaskService()
    {
        Type taskSchedulerType = Type.GetTypeFromProgID("Schedule.Service")!;
        dynamic taskService = Activator.CreateInstance(taskSchedulerType)!;
        taskService.Connect();
        return taskService;
    }

    private dynamic GetOrCreateWinhanceFolder(dynamic taskService)
    {
        dynamic rootFolder = taskService.GetFolder("\\");
        try
        {
            return rootFolder.GetFolder("Winhance");
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"[ScheduledTaskService] Winhance folder doesn't exist, creating: {ex.Message}");
            return rootFolder.CreateFolder("Winhance");
        }
    }

    private dynamic? GetWinhanceFolder(dynamic taskService)
    {
        try
        {
            dynamic rootFolder = taskService.GetFolder("\\");
            return rootFolder.GetFolder("Winhance");
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"[ScheduledTaskService] Winhance folder not found: {ex.Message}");
            return null;
        }
    }

    private async Task RemoveExistingTask(dynamic folder, string taskName)
    {
        try
        {
            var existingTask = folder.GetTask(taskName);
            if (existingTask != null)
            {
                folder.DeleteTask(taskName, 0);
                logService.LogInformation($"Deleted existing task: {taskName}");

                // Wait 2 seconds for Windows scheduled task cache to reset
                await Task.Delay(2000).ConfigureAwait(false);
                logService.LogInformation("Waited 2 seconds for task cache reset");
            }
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"[ScheduledTaskService] No existing task '{taskName}' to remove: {ex.Message}");
        }
    }


    private dynamic CreateTaskDefinition(dynamic taskService, string scriptPath, string command, string username, TaskTriggerType triggerType)
    {
        var taskDefinition = taskService.NewTask(0);

        // Settings
        var settings = taskDefinition.Settings;
        settings.Enabled = true;
        settings.DisallowStartIfOnBatteries = false;
        settings.StopIfGoingOnBatteries = false;
        settings.AllowDemandStart = true;

        // Trigger
        var triggers = taskDefinition.Triggers;
        var trigger = triggers.Create((int)triggerType);
        trigger.Enabled = true;

        if (triggerType == TaskTriggerType.Logon && !string.IsNullOrEmpty(username))
        {
            trigger.UserId = username;
        }

        // Action
        var actions = taskDefinition.Actions;
        var action = actions.Create(0); // TASK_ACTION_EXEC
        action.Path = "powershell.exe";
        action.Arguments = scriptPath != null
            ? $"-ExecutionPolicy Bypass -NoProfile -Command \"iex([IO.File]::ReadAllText('{scriptPath.Replace("'", "''")}'))\""
            : command;

        // Principal
        var principal = taskDefinition.Principal;
        if (!string.IsNullOrEmpty(username))
        {
            principal.UserId = username;
            principal.LogonType = 5; // Run whether logged in or not
            principal.RunLevel = 1; // Highest privileges
        }
        else
        {
            principal.UserId = "SYSTEM";
            principal.LogonType = 5;
            principal.RunLevel = 1;
        }

        return taskDefinition;
    }


    public async Task<OperationResult> EnableTaskAsync(string taskPath)
    {
        return await Task.Run(() => SetTaskEnabled(taskPath, true)).ConfigureAwait(false);
    }

    public async Task<OperationResult> DisableTaskAsync(string taskPath)
    {
        return await Task.Run(() => SetTaskEnabled(taskPath, false)).ConfigureAwait(false);
    }

    public async Task<bool?> IsTaskEnabledAsync(string taskPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var taskService = CreateTaskService();
                var (folderPath, taskName) = SplitTaskPath(taskPath);
                dynamic folder = taskService.GetFolder(folderPath);
                dynamic task = folder.GetTask(taskName);
                // State: 1 = Disabled, 3 = Ready, 4 = Running
                int state = (int)task.State;
                return (bool?)(state != 1);
            }
            catch (Exception ex)
            {
                logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                    $"Failed to query task state for {taskPath}: {ex.Message}");
                return null;
            }
        }).ConfigureAwait(false);
    }

    private OperationResult SetTaskEnabled(string taskPath, bool enabled)
    {
        try
        {
            var taskService = CreateTaskService();
            var (folderPath, taskName) = SplitTaskPath(taskPath);
            dynamic folder = taskService.GetFolder(folderPath);
            dynamic task = folder.GetTask(taskName);
            task.Enabled = enabled;
            logService.LogInformation($"{(enabled ? "Enabled" : "Disabled")} task: {taskPath}");
            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                $"Failed to {(enabled ? "enable" : "disable")} task {taskPath}: {ex.Message}");
            return OperationResult.Failed(ex.Message, ex);
        }
    }

    private static (string FolderPath, string TaskName) SplitTaskPath(string taskPath)
    {
        var lastSep = taskPath.LastIndexOf('\\');
        if (lastSep <= 0)
            return ("\\", taskPath.TrimStart('\\'));

        return (taskPath.Substring(0, lastSep), taskPath.Substring(lastSep + 1));
    }

    private void EnsureScriptFileExists(RemovalScript script)
    {
        if (!fileSystemService.FileExists(script.ActualScriptPath) && !string.IsNullOrEmpty(script.Content))
        {
            string? directoryPath = fileSystemService.GetDirectoryName(script.ActualScriptPath);
            if (directoryPath != null && !fileSystemService.DirectoryExists(directoryPath))
            {
                fileSystemService.CreateDirectory(directoryPath);
            }

            fileSystemService.WriteAllText(script.ActualScriptPath!, script.Content);
        }
    }

}
