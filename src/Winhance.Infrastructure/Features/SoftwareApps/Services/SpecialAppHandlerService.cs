using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Service for handling special applications that require custom removal processes.
    /// </summary>
    public class SpecialAppHandlerService : ISpecialAppHandlerService
    {
        private readonly ILogService _logService;
        private readonly SpecialAppHandler[] _handlers;
        private readonly ISystemServices _systemServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpecialAppHandlerService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="systemServices">The system services.</param>
        public SpecialAppHandlerService(ILogService logService, ISystemServices systemServices)
        {
            _logService = logService;
            _systemServices = systemServices;
            _handlers = SpecialAppHandler.GetPredefinedHandlers();
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveSpecialAppAsync(string appHandlerType)
        {
            try
            {
                _logService.LogInformation($"Removing special app with handler type: {appHandlerType}");
                
                bool success = false;
                
                switch (appHandlerType)
                {
                    case "Edge":
                        success = await RemoveEdgeAsync();
                        break;
                    case "OneDrive":
                        success = await RemoveOneDriveAsync();
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

                // Execute the script
                using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService, _systemServices);
                // No need to set execution policy as it's already done in the factory
                powerShell.AddScript(handler.RemovalScriptContent);
                await Task.Run(() => powerShell.Invoke());

                // Register scheduled task to prevent reinstallation
                using var taskPowerShell = PowerShellFactory.CreateWindowsPowerShell(_logService, _systemServices);
                var taskCommand = $@"
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
                _logService.LogInformation($"OneDrive removal script saved to {handler.ScriptPath}");

                // Execute the script
                using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService, _systemServices);
                // No need to set execution policy as it's already done in the factory
                powerShell.AddScript(handler.RemovalScriptContent);
                await Task.Run(() => powerShell.Invoke());

                // Register scheduled task to prevent reinstallation
                using var taskPowerShell = PowerShellFactory.CreateWindowsPowerShell(_logService, _systemServices);
                var taskCommand = $@"
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
        public SpecialAppHandler? GetHandler(string handlerType)
        {
            return _handlers.FirstOrDefault(h => h.HandlerType.Equals(handlerType, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public IEnumerable<SpecialAppHandler> GetAllHandlers()
        {
            return _handlers;
        }

        // SetExecutionPolicy is now handled by PowerShellFactory
    }
}