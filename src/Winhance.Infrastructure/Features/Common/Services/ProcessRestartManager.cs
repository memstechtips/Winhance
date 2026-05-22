using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ProcessRestartManager(
    IWindowsUIManagementService uiManagementService,
    IConfigImportState configImportState,
    ILogService logService) : IProcessRestartManager
{
    private int _suppressCount;

    /// <inheritdoc />
    public IDisposable SuppressRestarts()
    {
        Interlocked.Increment(ref _suppressCount);
        return new SuppressScope(this);
    }

    private sealed class SuppressScope(ProcessRestartManager owner) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Interlocked.Decrement(ref owner._suppressCount);
            }
        }
    }

    public async Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting)
    {
        if (_suppressCount > 0)
        {
            if (!string.IsNullOrEmpty(setting.RestartProcess))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{setting.RestartProcess}' (restarts suppressed - parent will restart)");
            if (!string.IsNullOrEmpty(setting.RestartService))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping service restart for '{setting.RestartService}' (restarts suppressed - parent will restart)");
            return;
        }

        if (configImportState.IsActive)
        {
            // For Explorer, fire the theme/settings broadcasts immediately so user sees
            // visual feedback during import — but defer the Explorer kill until end-of-import.
            if (!string.IsNullOrEmpty(setting.RestartProcess)
                && setting.RestartProcess.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                await uiManagementService.RefreshWindowsGUI(killExplorer: false).ConfigureAwait(false);
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Broadcast Explorer-refresh for '{setting.Id}' (kill deferred — config import mode)");
            }
            else if (!string.IsNullOrEmpty(setting.RestartProcess))
            {
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{setting.RestartProcess}' (config import mode - will restart at end)");
            }

            if (!string.IsNullOrEmpty(setting.RestartService))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping service restart for '{setting.RestartService}' (config import mode - will restart at end)");
            return;
        }

        if (!string.IsNullOrEmpty(setting.RestartProcess))
            await RestartProcessByNameAsync(setting.RestartProcess, setting.Id).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(setting.RestartService))
            RestartServiceByName(setting.RestartService, setting.Id);
    }

    public async Task FlushCoalescedRestartsAsync(IEnumerable<SettingDefinition> appliedSettings)
    {
        if (appliedSettings == null) return;

        var processes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var services = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in appliedSettings)
        {
            if (!string.IsNullOrEmpty(s.RestartProcess)) processes.Add(s.RestartProcess);
            if (!string.IsNullOrEmpty(s.RestartService)) services.Add(s.RestartService);
        }

        if (processes.Count == 0 && services.Count == 0) return;

        logService.Log(LogLevel.Info,
            $"[ProcessRestartManager] Flushing coalesced restarts: {processes.Count} process(es), {services.Count} service(s)");

        foreach (var process in processes)
            await RestartProcessByNameAsync(process, settingIdForLog: null).ConfigureAwait(false);

        foreach (var service in services)
            RestartServiceByName(service, settingIdForLog: null);
    }

    private async Task RestartProcessByNameAsync(string processName, string? settingIdForLog)
    {
        if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            var label = settingIdForLog is null
                ? "[ProcessRestartManager] Refreshing Windows UI (coalesced)"
                : $"[ProcessRestartManager] Refreshing Windows UI for setting '{settingIdForLog}'";
            logService.Log(LogLevel.Info, label);
            await uiManagementService.RefreshWindowsGUI(killExplorer: true).ConfigureAwait(false);
            return;
        }
        else if (processName.Equals("intl", StringComparison.OrdinalIgnoreCase))
        {
            logService.Log(LogLevel.Info,
                settingIdForLog != null
                    ? $"[ProcessRestartManager] Broadcasting regional setting change for '{settingIdForLog}'"
                    : "[ProcessRestartManager] Broadcasting regional setting change (coalesced)");
            uiManagementService.BroadcastRegionalSettingChange();
        }
        else
        {
            logService.Log(LogLevel.Info,
                settingIdForLog != null
                    ? $"[ProcessRestartManager] Restarting process '{processName}' for setting '{settingIdForLog}'"
                    : $"[ProcessRestartManager] Restarting process '{processName}' (coalesced)");
            try
            {
                uiManagementService.KillProcess(processName);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to restart process '{processName}': {ex.Message}");
            }
        }
    }

    private void RestartServiceByName(string serviceName, string? settingIdForLog)
    {
        logService.Log(LogLevel.Info,
            settingIdForLog != null
                ? $"[ProcessRestartManager] Restarting service '{serviceName}' for setting '{settingIdForLog}'"
                : $"[ProcessRestartManager] Restarting service '{serviceName}' (coalesced)");
        try
        {
            if (serviceName.Contains("*"))
            {
                var pattern = serviceName.Replace("*", "");
                var allServices = ServiceController.GetServices();
                try
                {
                    var matchingServices = allServices.Where(s =>
                        s.ServiceName.Contains(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var svc in matchingServices)
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
                finally
                {
                    foreach (var svc in allServices)
                        svc.Dispose();
                }
            }
            else
            {
                using var sc = new ServiceController(serviceName);
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
            logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to restart service '{serviceName}': {ex.Message}");
        }
    }
}
