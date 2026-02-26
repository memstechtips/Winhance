using System;
using System.Diagnostics;
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
        /// <summary>Maximum time to wait for Explorer to respawn after being killed.</summary>
        private static readonly TimeSpan ExplorerRespawnTimeout = TimeSpan.FromSeconds(10);

        /// <summary>Interval between Explorer process checks.</summary>
        private static readonly TimeSpan ExplorerPollInterval = TimeSpan.FromMilliseconds(500);

        /// <summary>Number of retry attempts if Explorer doesn't respawn.</summary>
        private const int ExplorerMaxRetries = 2;

        public async Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting)
        {
            if (!string.IsNullOrEmpty(setting.RestartProcess))
            {
                if (configImportState.IsActive)
                {
                    logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{setting.RestartProcess}' (config import mode - will restart at end)");
                }
                else if (setting.RestartProcess.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    await RestartExplorerWithRetryAsync(setting);
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

        /// <summary>
        /// Kills Explorer and waits for Windows to respawn it automatically.
        /// If Explorer doesn't come back within the timeout, manually starts it
        /// and retries up to <see cref="ExplorerMaxRetries"/> times.
        /// </summary>
        private async Task RestartExplorerWithRetryAsync(SettingDefinition setting)
        {
            logService.Log(LogLevel.Info, $"[ProcessRestartManager] Restarting Explorer for setting '{setting.Id}'");

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
                        // Last resort: try to start Explorer even if kill failed
                        TryStartExplorer();
                        return;
                    }
                    continue;
                }

                // Wait for Explorer to respawn (Windows normally restarts it automatically)
                if (await WaitForExplorerAsync())
                {
                    logService.Log(LogLevel.Info, "[ProcessRestartManager] Explorer restarted successfully");
                    return;
                }

                logService.Log(LogLevel.Warning,
                    $"[ProcessRestartManager] Explorer did not respawn within timeout (attempt {attempt + 1}/{ExplorerMaxRetries + 1})");

                // Manually start Explorer if it didn't respawn
                TryStartExplorer();

                if (await WaitForExplorerAsync())
                {
                    logService.Log(LogLevel.Info, "[ProcessRestartManager] Explorer started manually");
                    return;
                }
            }

            logService.Log(LogLevel.Error, "[ProcessRestartManager] Explorer failed to restart after all retry attempts");
        }

        /// <summary>
        /// Polls for the Explorer process to appear within <see cref="ExplorerRespawnTimeout"/>.
        /// </summary>
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
}
