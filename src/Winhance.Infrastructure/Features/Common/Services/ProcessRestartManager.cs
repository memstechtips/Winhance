using System;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class ProcessRestartManager(
        IWindowsUIManagementService uiManagementService,
        IConfigImportState configImportState,
        ILogService logService) : IProcessRestartManager
    {
        public async Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting)
        {
            if (!string.IsNullOrEmpty(setting.RestartProcess))
            {
                if (configImportState.IsActive)
                {
                    logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{setting.RestartProcess}' (config import mode - will restart at end)");
                }
                else
                {
                    logService.Log(LogLevel.Info, $"[ProcessRestartManager] Restarting process '{setting.RestartProcess}' for setting '{setting.Id}'");
                    try
                    {
                        uiManagementService.KillProcess(setting.RestartProcess);
                    }
                    catch (Exception ex)
                    {
                        logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to restart process '{setting.RestartProcess}': {ex.Message}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(setting.RestartService))
            {
                logService.Log(LogLevel.Info, $"[ProcessRestartManager] Restarting service '{setting.RestartService}' for setting '{setting.Id}'");
                try
                {
                    if (setting.RestartService.Contains("*"))
                    {
                        // Wildcard service names require enumeration
                        var pattern = setting.RestartService.Replace("*", "");
                        var allServices = ServiceController.GetServices();
                        var matchingServices = allServices.Where(s =>
                            s.ServiceName.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

                        foreach (var svc in matchingServices)
                        {
                            using (svc)
                            {
                                try
                                {
                                    if (svc.Status == ServiceControllerStatus.Running)
                                    {
                                        svc.Stop();
                                        svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                                        svc.Start();
                                    }
                                }
                                catch (Exception svcEx)
                                {
                                    logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to restart service '{svc.ServiceName}': {svcEx.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        using var sc = new ServiceController(setting.RestartService);
                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                            sc.Start();
                        }
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to restart service '{setting.RestartService}': {ex.Message}");
                }
            }
        }
    }
}
