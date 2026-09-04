using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

// Two contracts over one connection helper: Winhance's own tasks (IScheduledTaskService) and the state of tasks
// Windows owns (IScheduledTaskStateService).
internal class ScheduledTaskService(ILogService logService, IFileSystemService fileSystemService)
    : IScheduledTaskService, IScheduledTaskStateService
{
    private enum TaskTriggerType
    {
        Startup = 8,
        Logon = 9
    }

    // ERROR_FILE_NOT_FOUND as an HRESULT. The Task Scheduler COM API reports BOTH "no such folder" and
    // "no such task" with it, and both mean the same thing here: the task is not on this machine.
    private const int TaskNotFoundHResult = unchecked((int)0x80070002);

    // Releases every COM object taken through it, youngest first.
    private sealed class ComScope : IDisposable
    {
        private readonly List<object> _objects = new();

        [return: NotNullIfNotNull(nameof(comObject))]
        public object? Keep(object? comObject)
        {
            if (comObject is not null)
                _objects.Add(comObject);
            return comObject;
        }

        public void Dispose()
        {
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                try { Marshal.ReleaseComObject(_objects[i]); }
                catch { }
            }
        }
    }

    private dynamic Connect(ComScope com)
    {
        Type taskSchedulerType = Type.GetTypeFromProgID("Schedule.Service")!;
        dynamic taskService = com.Keep(Activator.CreateInstance(taskSchedulerType)!);

        taskService.Connect();
        return taskService;
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

                return await RegisterTaskInternal(script.Name, script.ActualScriptPath, triggerType).ConfigureAwait(false);
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
            using var com = new ComScope();
            try
            {
                object? found = GetWinhanceFolder(com, Connect(com));
                if (found is null) return OperationResult.Succeeded();
                dynamic folder = found;

                try
                {
                    object? existingTask = com.Keep((object)folder.GetTask(taskName));
                    if (existingTask is not null)
                    {
                        folder.DeleteTask(taskName, 0);
                        logService.LogInformation($"Unregistered task: {taskName}");
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"Task '{taskName}' not found: {ex.Message}");
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
            using var com = new ComScope();
            try
            {
                object? found = GetWinhanceFolder(com, Connect(com));
                if (found is null) return false;
                dynamic folder = found;

                return com.Keep((object)folder.GetTask(taskName)) is not null;
            }
            catch (Exception ex)
            {
                logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"Task '{taskName}' not registered: {ex.Message}");
                return false;
            }
        }).ConfigureAwait(false);
    }

    public async Task<OperationResult> RunScheduledTaskAsync(string taskName)
    {
        return await Task.Run(() =>
        {
            using var com = new ComScope();
            try
            {
                object? found = GetWinhanceFolder(com, Connect(com));
                if (found is null)
                {
                    logService.LogError($"Winhance task folder not found when trying to run: {taskName}");
                    return OperationResult.Failed("Winhance task folder not found");
                }
                dynamic folder = found;

                object? foundTask = com.Keep((object)folder.GetTask(taskName));
                if (foundTask is null)
                {
                    logService.LogError($"Task not found: {taskName}");
                    return OperationResult.Failed($"Task not found: {taskName}");
                }
                dynamic task = foundTask;

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

    private async Task<OperationResult> RegisterTaskInternal(string taskName, string scriptPath, TaskTriggerType triggerType)
    {
        using var com = new ComScope();
        dynamic taskService = Connect(com);
        dynamic folder = GetOrCreateWinhanceFolder(com, taskService);


        await RemoveExistingTask(com, folder, taskName).ConfigureAwait(false);

        dynamic taskDefinition = CreateTaskDefinition(com, taskService, scriptPath, triggerType);

        folder.RegisterTaskDefinition(
            taskName,
            taskDefinition,
            6,      // TASK_CREATE_OR_UPDATE
            null,   // user: always SYSTEM
            null,   // password
            5,      // TASK_LOGON_SERVICE_ACCOUNT
            null
        );

        logService.LogInformation($"Registered task: {taskName} as SYSTEM");
        return OperationResult.Succeeded();
    }

    private object GetOrCreateWinhanceFolder(ComScope com, dynamic taskService)
    {
        dynamic rootFolder = com.Keep((object)taskService.GetFolder("\\"));
        try
        {
            return com.Keep((object)rootFolder.GetFolder("Winhance"));
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"Winhance folder doesn't exist, creating: {ex.Message}");
            return com.Keep((object)rootFolder.CreateFolder("Winhance"));
        }
    }

    private object? GetWinhanceFolder(ComScope com, dynamic taskService)
    {
        try
        {
            dynamic rootFolder = com.Keep((object)taskService.GetFolder("\\"));
            return com.Keep((object)rootFolder.GetFolder("Winhance"));
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"Winhance folder not found: {ex.Message}");
            return null;
        }
    }

    private async Task RemoveExistingTask(ComScope com, dynamic folder, string taskName)
    {
        try
        {
            object? existingTask = com.Keep((object)folder.GetTask(taskName));
            if (existingTask is not null)
            {
                folder.DeleteTask(taskName, 0);
                logService.LogInformation($"Deleted existing task: {taskName}");

                // The Task Scheduler serves a stale task list for a moment after a delete.
                await Task.Delay(2000).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Debug, $"No existing task '{taskName}' to remove: {ex.Message}");
        }
    }

    private static dynamic CreateTaskDefinition(ComScope com, dynamic taskService, string scriptPath, TaskTriggerType triggerType)
    {
        dynamic taskDefinition = com.Keep((object)taskService.NewTask(0));

        dynamic settings = com.Keep((object)taskDefinition.Settings);
        settings.Enabled = true;
        settings.DisallowStartIfOnBatteries = false;
        settings.StopIfGoingOnBatteries = false;
        settings.AllowDemandStart = true;

        dynamic triggers = com.Keep((object)taskDefinition.Triggers);
        dynamic trigger = com.Keep((object)triggers.Create((int)triggerType));
        trigger.Enabled = true;

        dynamic actions = com.Keep((object)taskDefinition.Actions);
        dynamic action = com.Keep((object)actions.Create(0));   // TASK_ACTION_EXEC
        action.Path = "powershell.exe";
        action.Arguments = $"-ExecutionPolicy Bypass -NoProfile -Command \"iex([IO.File]::ReadAllText('{scriptPath.Replace("'", "''")}'))\"";

        dynamic principal = com.Keep((object)taskDefinition.Principal);
        principal.UserId = "SYSTEM";
        principal.LogonType = 5;    // Run whether logged in or not
        principal.RunLevel = 1;     // Highest privileges

        return taskDefinition;
    }

    public IReadOnlyDictionary<string, bool?> GetTasksEnabled(IReadOnlyCollection<string> taskPaths)
    {
        var results = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
        if (taskPaths.Count == 0)
            return results;

        using var com = new ComScope();
        try
        {
            // ONE connection for the batch: creating a Schedule.Service instance is an out-of-process COM
            // activation, and doing it per path is what made a page navigation open N of them at once.
            dynamic taskService = Connect(com);
            foreach (var taskPath in taskPaths)
                results[taskPath] = ReadTaskEnabled(com, taskService, taskPath);
        }
        catch (Exception ex)
        {
            // Every requested path resolves to "unknown" rather than being absent, so the detection context
            // does not read a connection failure as "never asked".
            logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                $"Failed to connect to the Task Scheduler; {taskPaths.Count} task state(s) unavailable: {ex.Message}");
            foreach (var taskPath in taskPaths)
                results[taskPath] = null;
        }

        return results;
    }

    public OperationResult SetTaskEnabled(string taskPath, bool enabled)
    {
        using var com = new ComScope();
        try
        {
            dynamic taskService = Connect(com);
            var (folderPath, taskName) = SplitTaskPath(taskPath);
            dynamic folder = com.Keep((object)taskService.GetFolder(folderPath));
            dynamic task = com.Keep((object)folder.GetTask(taskName));
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

    // Never throws: a missing task and a failed read both resolve to null, matching how the detection
    // context models "absent".
    private bool? ReadTaskEnabled(ComScope com, dynamic taskService, string taskPath)
    {
        try
        {
            var (folderPath, taskName) = SplitTaskPath(taskPath);
            dynamic folder = com.Keep((object)taskService.GetFolder(folderPath));
            dynamic task = com.Keep((object)folder.GetTask(taskName));
            // State: 1 = Disabled, 3 = Ready, 4 = Running
            int state = (int)task.State;
            return state != 1;
        }
        catch (Exception ex) when (IsTaskNotFound(ex))
        {
            // Normal: the task is not installed on this PC (Recall, for one, is absent from most). Reporting
            // it at Warning put a red herring in every user's log.
            logService.Log(Core.Features.Common.Enums.LogLevel.Debug,
                $"Scheduled task {taskPath} is not present on this machine");
            return null;
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Warning,
                $"Failed to query task state for {taskPath}: {ex.Message}");
            return null;
        }
    }

    // The chain is walked because dynamic dispatch may wrap the COMException carrying the HRESULT.
    private static bool IsTaskNotFound(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is FileNotFoundException || ex.HResult == TaskNotFoundHResult)
                return true;
        }

        return false;
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
        if (!fileSystemService.FileExists(script.ActualScriptPath!) && !string.IsNullOrEmpty(script.Content))
        {
            string? directoryPath = fileSystemService.GetDirectoryName(script.ActualScriptPath!);
            if (directoryPath != null && !fileSystemService.DirectoryExists(directoryPath))
            {
                fileSystemService.CreateDirectory(directoryPath);
            }

            fileSystemService.WriteAllText(script.ActualScriptPath!, script.Content);
        }
    }
}
