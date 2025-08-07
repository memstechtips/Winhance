using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Service for handling special applications that require custom removal processes.
    /// </summary>
    public class SpecialAppHandlerService : ISpecialAppHandlerService
    {
        private readonly ILogService _logService;
        private readonly SpecialAppHandler[] _handlers;
        private readonly IPowerShellDetectionService _powerShellDetectionService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpecialAppHandlerService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="powerShellDetectionService">The PowerShell detection service.</param>
        public SpecialAppHandlerService(ILogService logService, IPowerShellDetectionService powerShellDetectionService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _powerShellDetectionService = powerShellDetectionService ?? throw new ArgumentNullException(nameof(powerShellDetectionService));
            _handlers = SpecialAppHandler.GetPredefinedHandlers();
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveSpecialAppAsync(string appHandlerType)
        {
            try
            {
                _logService.LogInformation(
                    $"Removing special app with handler type: {appHandlerType}"
                );

                bool success = false;

                switch (appHandlerType)
                {
                    case "Edge":
                        success = await RemoveEdgeAsync();
                        break;
                    case "OneDrive":
                        success = await RemoveOneDriveAsync();
                        break;
                    case "OneNote":
                        success = await RemoveOneNoteAsync();
                        break;
                    default:
                        _logService.LogError($"Unknown special handler type: {appHandlerType}");
                        return false;
                }

                if (success)
                {
                    _logService.LogSuccess($"Successfully removed special app: {appHandlerType}");
                }
                else
                {
                    _logService.LogError($"Failed to remove special app: {appHandlerType}");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error removing special app: {appHandlerType}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveEdgeAsync()
        {
            try
            {
                _logService.LogInformation("Starting Edge removal process");

                var handler = GetHandler("Edge");
                if (handler == null)
                {
                    _logService.LogError("Edge handler not found");
                    return false;
                }

                // Store the Edge removal script
                var scriptPath = Path.GetDirectoryName(handler.ScriptPath);
                Directory.CreateDirectory(scriptPath);

                File.WriteAllText(handler.ScriptPath, handler.RemovalScriptContent);
                _logService.LogInformation($"Edge removal script saved to {handler.ScriptPath}");

                // Execute the saved script file directly
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{handler.ScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                _logService.LogInformation($"Executing Edge removal script: {handler.ScriptPath}");
                var process = System.Diagnostics.Process.Start(processStartInfo);

                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(output))
                    {
                        _logService.LogInformation($"Script output: {output}");
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        _logService.LogError($"Script error: {error}");
                    }
                }
                else
                {
                    _logService.LogError("Failed to start PowerShell process for Edge removal");
                }

                // Register scheduled task to prevent reinstallation
                using var taskPowerShell = PowerShell.Create();
                var taskCommand =
                    $@"
                Register-ScheduledTask -TaskName '{handler.ScheduledTaskName}' `
                -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-ExecutionPolicy Bypass -File ""{handler.ScriptPath}""') `
                -Trigger (New-ScheduledTaskTrigger -AtStartup) `
                -User 'SYSTEM' `
                -RunLevel Highest `
                -Settings (New-ScheduledTaskSettingsSet -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries) `
                -Force
                ";
                taskPowerShell.AddScript(taskCommand);
                await Task.Run(() => taskPowerShell.Invoke());

                _logService.LogSuccess("Edge removal completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Edge removal failed", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveOneDriveAsync()
        {
            try
            {
                _logService.LogInformation("Starting OneDrive removal process");

                var handler = GetHandler("OneDrive");
                if (handler == null)
                {
                    _logService.LogError("OneDrive handler not found");
                    return false;
                }

                // Store the OneDrive removal script
                var scriptPath = Path.GetDirectoryName(handler.ScriptPath);
                Directory.CreateDirectory(scriptPath);

                File.WriteAllText(handler.ScriptPath, handler.RemovalScriptContent);
                _logService.LogInformation(
                    $"OneDrive removal script saved to {handler.ScriptPath}"
                );

                // Execute the saved script file directly
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{handler.ScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                _logService.LogInformation(
                    $"Executing OneDrive removal script: {handler.ScriptPath}"
                );
                var process = System.Diagnostics.Process.Start(processStartInfo);

                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(output))
                    {
                        _logService.LogInformation($"Script output: {output}");
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        _logService.LogError($"Script error: {error}");
                    }
                }
                else
                {
                    _logService.LogError("Failed to start PowerShell process for OneDrive removal");
                }

                // Register scheduled task to prevent reinstallation
                using var taskPowerShell = PowerShell.Create();
                var taskCommand =
                    $@"
                Register-ScheduledTask -TaskName '{handler.ScheduledTaskName}' `
                -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-ExecutionPolicy Bypass -File ""{handler.ScriptPath}""') `
                -Trigger (New-ScheduledTaskTrigger -AtStartup) `
                -User 'SYSTEM' `
                -RunLevel Highest `
                -Settings (New-ScheduledTaskSettingsSet -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries) `
                -Force
                ";
                taskPowerShell.AddScript(taskCommand);
                await Task.Run(() => taskPowerShell.Invoke());

                _logService.LogSuccess("OneDrive removal completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("OneDrive removal failed", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveOneNoteAsync()
        {
            try
            {
                _logService.LogInformation("Starting OneNote removal process");

                var handler = GetHandler("OneNote");
                if (handler == null)
                {
                    _logService.LogError("OneNote handler not found");
                    return false;
                }

                // Store the OneNote removal script
                var scriptPath = Path.GetDirectoryName(handler.ScriptPath);
                Directory.CreateDirectory(scriptPath);

                File.WriteAllText(handler.ScriptPath, handler.RemovalScriptContent);
                _logService.LogInformation($"OneNote removal script saved to {handler.ScriptPath}");

                // Execute the saved script file directly
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{handler.ScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                _logService.LogInformation(
                    $"Executing OneNote removal script: {handler.ScriptPath}"
                );
                var process = System.Diagnostics.Process.Start(processStartInfo);

                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(output))
                    {
                        _logService.LogInformation($"Script output: {output}");
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        _logService.LogError($"Script error: {error}");
                    }
                }
                else
                {
                    _logService.LogError("Failed to start PowerShell process for OneNote removal");
                }

                // Register scheduled task to prevent reinstallation
                using var taskPowerShell = PowerShell.Create();
                var taskCommand =
                    $@"
                Register-ScheduledTask -TaskName '{handler.ScheduledTaskName}' `
                -Action (New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-ExecutionPolicy Bypass -File ""{handler.ScriptPath}""') `
                -Trigger (New-ScheduledTaskTrigger -AtStartup) `
                -User 'SYSTEM' `
                -RunLevel Highest `
                -Settings (New-ScheduledTaskSettingsSet -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries) `
                -Force
                ";
                taskPowerShell.AddScript(taskCommand);
                await Task.Run(() => taskPowerShell.Invoke());

                _logService.LogSuccess("OneNote removal completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("OneNote removal failed", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public SpecialAppHandler? GetHandler(string handlerType)
        {
            return _handlers.FirstOrDefault(h =>
                h.HandlerType.Equals(handlerType, StringComparison.OrdinalIgnoreCase)
            );
        }

        /// <inheritdoc/>
        public IEnumerable<SpecialAppHandler> GetAllHandlers()
        {
            return _handlers;
        }
    }
}
