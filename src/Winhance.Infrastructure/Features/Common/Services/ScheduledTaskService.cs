using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ScheduledTaskService(ILogService logService) : IScheduledTaskService
{
    private enum TaskTriggerType
    {
        Startup = 8,
        Logon = 9
    }

    public async Task<bool> RegisterScheduledTaskAsync(RemovalScript script)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (script?.ActualScriptPath == null)
                {
                    logService.LogError("Script or script path is null");
                    return false;
                }

                EnsureScriptFileExists(script);

                var triggerType = script.RunOnStartup ? TaskTriggerType.Startup : TaskTriggerType.Logon;

                return RegisterTaskInternal(script.Name, script.ActualScriptPath, null, triggerType);
            }
            catch (Exception ex)
            {
                logService.LogError($"Error registering scheduled task for {script?.Name}", ex);
                return false;
            }
        }).ConfigureAwait(false);
    }


    public async Task<bool> UnregisterScheduledTaskAsync(string taskName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var taskService = CreateTaskService();
                var folder = GetWinhanceFolder(taskService);

                if (folder == null) return true;

                try
                {
                    var existingTask = folder.GetTask(taskName);
                    if (existingTask != null)
                    {
                        folder.DeleteTask(taskName, 0);
                        logService.LogInformation($"Unregistered task: {taskName}");
                    }
                }
                catch
                {
                    // Task doesn't exist
                }

                return true;
            }
            catch (Exception ex)
            {
                logService.LogError($"Error unregistering task: {taskName}", ex);
                return false;
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
            catch
            {
                return false;
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> RunScheduledTaskAsync(string taskName)
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
                    return false;
                }

                var task = folder.GetTask(taskName);
                if (task == null)
                {
                    logService.LogError($"Task not found: {taskName}");
                    return false;
                }

                task.Run(null);
                logService.LogInformation($"Started task: {taskName}");
                return true;
            }
            catch (Exception ex)
            {
                logService.LogError($"Error running task: {taskName}", ex);
                return false;
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> CreateUserLogonTaskAsync(string taskName, string command, string username, bool deleteAfterRun = true)
    {
        return await Task.Run(() =>
        {
            try
            {
                return RegisterTaskInternal(taskName, null, username, TaskTriggerType.Logon, command);
            }
            catch (Exception ex)
            {
                logService.LogError($"Error creating user logon task: {taskName}", ex);
                return false;
            }
        }).ConfigureAwait(false);
    }

    private bool RegisterTaskInternal(string taskName, string? scriptPath, string? username, TaskTriggerType triggerType, string? command = null)
    {
        var taskService = CreateTaskService();
        var folder = GetOrCreateWinhanceFolder(taskService);

        RemoveExistingTask(folder, taskName);

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
        return true;
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
        catch
        {
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
        catch
        {
            return null;
        }
    }

    private void RemoveExistingTask(dynamic folder, string taskName)
    {
        try
        {
            var existingTask = folder.GetTask(taskName);
            if (existingTask != null)
            {
                folder.DeleteTask(taskName, 0);
                logService.LogInformation($"Deleted existing task: {taskName}");

                // Wait 2 seconds for Windows scheduled task cache to reset
                System.Threading.Thread.Sleep(2000);
                logService.LogInformation("Waited 2 seconds for task cache reset");
            }
        }
        catch
        {
            // Task doesn't exist
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
            ? $"-ExecutionPolicy Bypass -File \"{scriptPath}\""
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


    public async Task<bool> EnableTaskAsync(string taskPath)
    {
        return await Task.Run(() => SetTaskEnabled(taskPath, true)).ConfigureAwait(false);
    }

    public async Task<bool> DisableTaskAsync(string taskPath)
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

    private bool SetTaskEnabled(string taskPath, bool enabled)
    {
        try
        {
            var taskService = CreateTaskService();
            var (folderPath, taskName) = SplitTaskPath(taskPath);
            dynamic folder = taskService.GetFolder(folderPath);
            dynamic task = folder.GetTask(taskName);
            task.Enabled = enabled;
            logService.LogInformation($"{(enabled ? "Enabled" : "Disabled")} task: {taskPath}");
            return true;
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                $"Failed to {(enabled ? "enable" : "disable")} task {taskPath}: {ex.Message}");
            return false;
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
        if (!File.Exists(script.ActualScriptPath) && !string.IsNullOrEmpty(script.Content))
        {
            string? directoryPath = Path.GetDirectoryName(script.ActualScriptPath);
            if (directoryPath != null && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(script.ActualScriptPath!, script.Content);
        }
    }

}