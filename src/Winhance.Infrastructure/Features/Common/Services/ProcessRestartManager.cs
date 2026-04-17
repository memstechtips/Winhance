using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <summary>Maximum time to wait for Explorer to respawn after being killed.</summary>
    private static readonly TimeSpan ExplorerRespawnTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Interval between Explorer process checks.</summary>
    private static readonly TimeSpan ExplorerPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Number of retry attempts if Explorer doesn't respawn.</summary>
    private const int ExplorerMaxRetries = 2;

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
            if (!string.IsNullOrEmpty(setting.RestartProcess))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{setting.RestartProcess}' (config import mode - will restart at end)");
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
            await RestartExplorerWithRetryAsync(settingIdForLog).ConfigureAwait(false);
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

    /// <summary>
    /// Kills Explorer and waits for Windows to respawn it automatically.
    /// If Explorer doesn't come back within the timeout, manually starts it
    /// and retries up to <see cref="ExplorerMaxRetries"/> times.
    /// </summary>
    private async Task RestartExplorerWithRetryAsync(string? settingIdForLog)
    {
        logService.Log(LogLevel.Info,
            settingIdForLog != null
                ? $"[ProcessRestartManager] Restarting Explorer for setting '{settingIdForLog}'"
                : "[ProcessRestartManager] Restarting Explorer (coalesced)");

        for (int attempt = 0; attempt <= ExplorerMaxRetries; attempt++)
        {
            try
            {
                uiManagementService.KillProcess("explorer");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ProcessRestartManager] Failed to kill Explorer (attempt {attempt + 1}): {ex.Message}");
                if (attempt == ExplorerMaxRetries)
                {
                    TryStartExplorer();
                    return;
                }
                continue;
            }

            if (await WaitForExplorerAsync())
            {
                logService.Log(LogLevel.Info, "[ProcessRestartManager] Explorer restarted successfully");
                return;
            }

            logService.Log(LogLevel.Warning,
                $"[ProcessRestartManager] Explorer did not respawn within timeout (attempt {attempt + 1}/{ExplorerMaxRetries + 1})");

            TryStartExplorer();

            if (await WaitForExplorerAsync())
            {
                logService.Log(LogLevel.Info, "[ProcessRestartManager] Explorer started manually");
                return;
            }
        }

        logService.Log(LogLevel.Error, "[ProcessRestartManager] Explorer failed to restart after all retry attempts");
    }

    private async Task<bool> WaitForExplorerAsync()
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < ExplorerRespawnTimeout)
        {
            await Task.Delay(ExplorerPollInterval);
            if (uiManagementService.IsProcessRunning("explorer"))
                return true;
        }
        return false;
    }

    private void TryStartExplorer()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[ProcessRestartManager] Failed to manually start Explorer: {ex.Message}");
        }
    }
}
