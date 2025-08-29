using System;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for registering and managing scheduled tasks for script execution.
    /// </summary>
    public class ScheduledTaskService : IScheduledTaskService
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledTaskService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        public ScheduledTaskService(ILogService logService)
        {
            _logService = logService;
        }

        /// <inheritdoc/>
        public async Task<bool> RegisterScheduledTaskAsync(RemovalScript script)
        {
            try
            {
                _logService.LogInformation($"Registering scheduled task for script: {script?.Name ?? "Unknown"}");

                // Ensure the script and script path are valid
                if (script == null)
                {
                    _logService.LogError("Script object is null");
                    return false;
                }

                // Ensure the script name is not empty
                if (string.IsNullOrEmpty(script.Name))
                {
                    _logService.LogError("Script name is empty");
                    return false;
                }

                // Ensure the script path exists
                string scriptPath = script.ScriptPath;
                if (!File.Exists(scriptPath))
                {
                    _logService.LogError($"Script file not found at: {scriptPath}");

                    // Try to save the script if it doesn't exist but has content
                    if (!string.IsNullOrEmpty(script.Content))
                    {
                        try
                        {
                            string directoryPath = Path.GetDirectoryName(scriptPath);
                            if (!Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                            }

                            File.WriteAllText(scriptPath, script.Content);
                            _logService.LogInformation($"Created script file at: {scriptPath}");
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError($"Failed to create script file: {ex.Message}");
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                // Create the scheduled task using a direct PowerShell command
                string taskName = !string.IsNullOrEmpty(script.TargetScheduledTaskName)
                    ? script.TargetScheduledTaskName
                    : $"Winhance\\{script.Name}";

                // Create a simple PowerShell script that registers the task
                string psScript = $@"
# Register the scheduled task
$scriptPath = '{scriptPath.Replace("'", "''")}' # Escape single quotes
$taskName = '{taskName.Replace("'", "''")}'

# Forcefully delete any existing task to ensure it's completely recreated
try {{
    # First try to stop the task if it's running
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {{
        Write-Output ""Found existing task: $taskName""
        
        # Try to stop the task if it's running
        $taskState = (Get-ScheduledTask -TaskName $taskName).State
        if ($taskState -eq 'Running') {{
            Write-Output ""Stopping running task: $taskName""
            Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        }}
        
        # Forcefully unregister the task
        Write-Output ""Forcefully removing existing task: $taskName""
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction Stop
        
        # Add a small delay to ensure the task is fully removed
        Start-Sleep -Seconds 1
    }}
}} catch {{
    Write-Output ""Error removing existing task: $($_.Exception.Message)""
    # Continue anyway, as we'll try to register with -Force
}}

# Create the action to run the PowerShell script
try {{
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ""-ExecutionPolicy Bypass -File `""$scriptPath`""""
    $trigger = New-ScheduledTaskTrigger -AtStartup
    $settings = New-ScheduledTaskSettingsSet -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries
    
    # Register the task
    Register-ScheduledTask -TaskName $taskName `
        -Action $action `
        -Trigger $trigger `
        -User ""SYSTEM"" `
        -RunLevel Highest `
        -Settings $settings `
        -Force -ErrorAction Stop
    
    Write-Output ""Successfully registered scheduled task: $taskName""
    exit 0
}} catch {{
    Write-Output ""Error registering scheduled task: $($_.Exception.Message)""
    exit 1
}}
";

                // Save the script to a temporary file
                string tempScriptPath = Path.Combine(Path.GetTempPath(), $"RegisterTask_{Guid.NewGuid()}.ps1");
                File.WriteAllText(tempScriptPath, psScript);

                try
                {
                    // Execute the script with elevated privileges
                    using var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"";
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.Verb = "runas"; // Run as administrator
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

                    process.Start();
                    await process.WaitForExitAsync();

                    // Check if the process exited successfully
                    if (process.ExitCode == 0)
                    {
                        _logService.LogSuccess($"Successfully registered scheduled task: {taskName}");
                        return true;
                    }
                    else
                    {
                        _logService.LogError($"Failed to register scheduled task: {taskName}, exit code: {process.ExitCode}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error executing scheduled task registration script: {ex.Message}");
                    return false;
                }
                finally
                {
                    // Clean up the temporary script
                    try
                    {
                        if (File.Exists(tempScriptPath))
                        {
                            File.Delete(tempScriptPath);
                        }
                    }
                    catch
                    {
                        // Ignore errors when deleting the temp file
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error registering scheduled task for script: {script?.Name ?? "Unknown"}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UnregisterScheduledTaskAsync(string taskName)
        {
            try
            {
                _logService.LogInformation($"Unregistering scheduled task: {taskName}");

                // Create a simple PowerShell script that unregisters the task
                string psScript = $@"
# Unregister the scheduled task
$taskName = '{taskName.Replace("'", "''")}'

try {{
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {{
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction Stop
        Write-Output ""Successfully unregistered task: $taskName""
        exit 0
    }} else {{
        Write-Output ""Task not found: $taskName""
        exit 0  # Not an error if the task doesn't exist
    }}
}} catch {{
    Write-Output ""Error unregistering task: $($_.Exception.Message)""
    exit 1
}}
";

                // Save the script to a temporary file
                string tempScriptPath = Path.Combine(Path.GetTempPath(), $"UnregisterTask_{Guid.NewGuid()}.ps1");
                File.WriteAllText(tempScriptPath, psScript);

                try
                {
                    // Execute the script with elevated privileges
                    using var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"";
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.Verb = "runas"; // Run as administrator
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;

                    process.Start();
                    await process.WaitForExitAsync();

                    // Check if the process exited successfully
                    if (process.ExitCode == 0)
                    {
                        _logService.LogSuccess($"Successfully unregistered scheduled task: {taskName}");
                        return true;
                    }
                    else
                    {
                        _logService.LogError($"Failed to unregister scheduled task: {taskName}, exit code: {process.ExitCode}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error executing scheduled task unregistration script: {ex.Message}");
                    return false;
                }
                finally
                {
                    // Clean up the temporary script
                    try
                    {
                        if (File.Exists(tempScriptPath))
                        {
                            File.Delete(tempScriptPath);
                        }
                    }
                    catch
                    {
                        // Ignore errors when deleting the temp file
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error unregistering scheduled task: {taskName}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsTaskRegisteredAsync(string taskName)
        {
            try
            {
                // Create a simple PowerShell script that checks if the task exists
                string psScript = $@"
# Check if the scheduled task exists
$taskName = '{taskName.Replace("'", "''")}'

try {{
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($existingTask) {{
        Write-Output ""Task exists: $taskName""
        exit 0  # Exit code 0 means the task exists
    }} else {{
        Write-Output ""Task does not exist: $taskName""
        exit 1  # Exit code 1 means the task does not exist
    }}
}} catch {{
    Write-Output ""Error checking task: $($_.Exception.Message)""
    exit 2  # Exit code 2 means an error occurred
}}
";

                // Save the script to a temporary file
                string tempScriptPath = Path.Combine(Path.GetTempPath(), $"CheckTask_{Guid.NewGuid()}.ps1");
                File.WriteAllText(tempScriptPath, psScript);

                try
                {
                    // Execute the script
                    using var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    await process.WaitForExitAsync();

                    // Check the exit code to determine if the task exists
                    return process.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error executing scheduled task check script: {ex.Message}");
                    return false;
                }
                finally
                {
                    // Clean up the temporary script
                    try
                    {
                        if (File.Exists(tempScriptPath))
                        {
                            File.Delete(tempScriptPath);
                        }
                    }
                    catch
                    {
                        // Ignore errors when deleting the temp file
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking if task exists: {taskName}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> CreateUserLogonTaskAsync(string taskName, string command, string username, bool deleteAfterRun = true)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logService.LogInformation($"Creating user logon task: {taskName} for user: {username} using native C# Task Scheduler API");

                    // Create Task Scheduler COM object
                    Type taskSchedulerType = Type.GetTypeFromProgID("Schedule.Service");
                    dynamic taskService = Activator.CreateInstance(taskSchedulerType);
                    taskService.Connect();

                    // Get or create the Winhance task folder
                    dynamic rootFolder = taskService.GetFolder("\\");
                    dynamic winhanceFolder;
                    try
                    {
                        // Try to get existing Winhance folder
                        winhanceFolder = rootFolder.GetFolder("Winhance");
                    }
                    catch
                    {
                        // Create Winhance folder if it doesn't exist
                        winhanceFolder = rootFolder.CreateFolder("Winhance");
                        _logService.LogInformation("Created Winhance task folder");
                    }

                    // Remove existing task if it exists
                    try
                    {
                        dynamic existingTask = winhanceFolder.GetTask(taskName);
                        if (existingTask != null)
                        {
                            _logService.LogInformation($"Removing existing task: {taskName}");
                            winhanceFolder.DeleteTask(taskName, 0);
                        }
                    }
                    catch
                    {
                        // Task doesn't exist, which is fine
                    }

                    // Create a new task definition
                    dynamic taskDefinition = taskService.NewTask(0);

                    // Set task settings
                    dynamic settings = taskDefinition.Settings;
                    settings.Enabled = true;
                    settings.AllowDemandStart = true;
                    settings.AllowHardTerminate = true;
                    settings.DisallowStartIfOnBatteries = false;
                    settings.StopIfGoingOnBatteries = false;

                    // Create logon trigger for the specific user
                    dynamic triggers = taskDefinition.Triggers;
                    dynamic trigger = triggers.Create(9); // TASK_TRIGGER_LOGON = 9
                    trigger.UserId = username;
                    trigger.Enabled = true;

                    // Create action to execute powershell.exe with the command
                    dynamic actions = taskDefinition.Actions;
                    dynamic action = actions.Create(0); // TASK_ACTION_EXEC = 0
                    action.Path = "powershell.exe";
                    action.Arguments = command;

                    // Set principal to run as SYSTEM whether user is logged in or not
                    dynamic principal = taskDefinition.Principal;
                    principal.UserId = "S-1-5-18"; // SYSTEM account SID
                    principal.LogonType = 5; // TASK_LOGON_SERVICE_ACCOUNT = 5 (run whether user is logged on or not)
                    principal.RunLevel = 1; // TASK_RUNLEVEL_HIGHEST = 1 (elevated privileges)

                    // Register the task with simplified parameters
                    dynamic registeredTask = winhanceFolder.RegisterTaskDefinition(
                        taskName,
                        taskDefinition,
                        6, // TASK_CREATE_OR_UPDATE = 6
                        null, // user (null to use principal.UserId)
                        null, // password
                        0, // logon type (use principal.LogonType)
                        null // sddl
                    );

                    _logService.LogSuccess($"Successfully created user logon task: {taskName} for user: {username}");

                    return true;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error creating user logon task: {taskName} for user: {username}", ex);
                    return false;
                }
            });
        }
    }
}