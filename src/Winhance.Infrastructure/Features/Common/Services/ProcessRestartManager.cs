using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Catalog;

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

    public Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting)
        => HandleRestartsAsync(setting.RestartProcess, setting.RestartService, setting.Id);

    /// <inheritdoc />
    public Task HandleProcessAndServiceRestartsAsync(Setting setting)
    {
        // The catalog Setting unifies the def's separate RestartProcess/RestartService into one
        // ApplyBehavior.Restart RestartTarget (lossless - no setting sets both; RestartTargetCatalogEquivalence
        // Tests). Reuse the proven CollectRestartTargets extraction (0/1 process, 0/1 service), then run the
        // identical restart logic the def overload runs.
        var (processes, services) = CollectRestartTargets(new[] { setting });
        return HandleRestartsAsync(processes.FirstOrDefault(), services.FirstOrDefault(), setting.Id);
    }

    // Shared restart logic for both single-setting overloads. Behaviour-identical to the old
    // HandleProcessAndServiceRestartsAsync(SettingDefinition) body (process/service handled independently),
    // just parameterised by the extracted (process, service, id) so the catalog Setting overload can reuse it.
    private async Task HandleRestartsAsync(string? restartProcess, string? restartService, string settingId)
    {
        if (_suppressCount > 0)
        {
            if (!string.IsNullOrEmpty(restartProcess))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{restartProcess}' (restarts suppressed - parent will restart)");
            if (!string.IsNullOrEmpty(restartService))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping service restart for '{restartService}' (restarts suppressed - parent will restart)");
            return;
        }

        if (configImportState.IsActive)
        {
            // For Explorer, fire the theme/settings broadcasts immediately so user sees
            // visual feedback during import - but defer the Explorer kill until end-of-import.
            if (!string.IsNullOrEmpty(restartProcess)
                && restartProcess.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                await uiManagementService.RefreshWindowsGUI(killExplorer: false).ConfigureAwait(false);
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Broadcast Explorer-refresh for '{settingId}' (kill deferred - config import mode)");
            }
            else if (!string.IsNullOrEmpty(restartProcess))
            {
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{restartProcess}' (config import mode - will restart at end)");
            }

            if (!string.IsNullOrEmpty(restartService))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping service restart for '{restartService}' (config import mode - will restart at end)");
            return;
        }

        if (!string.IsNullOrEmpty(restartProcess))
            await RestartProcessByNameAsync(restartProcess, settingId).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(restartService))
            RestartServiceByName(restartService, settingId);
    }

    public async Task FlushCoalescedRestartsAsync(IEnumerable<SettingDefinition> appliedSettings)
    {
        if (appliedSettings == null) return;
        var (processes, services) = CollectRestartTargets(appliedSettings);
        await FlushSetsAsync(processes, services).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task FlushCoalescedRestartsAsync(IEnumerable<Setting> appliedSettings)
    {
        if (appliedSettings == null) return;
        var (processes, services) = CollectRestartTargets(appliedSettings);
        await FlushSetsAsync(processes, services).ConfigureAwait(false);
    }

    // Collect the distinct (process, service) restart targets across a batch. Static + internal so the
    // catalog-equivalence test can compare the two overloads over the whole population without triggering
    // real restarts. The def keeps RestartProcess/RestartService as separate strings.
    internal static (HashSet<string> Processes, HashSet<string> Services) CollectRestartTargets(
        IEnumerable<SettingDefinition> settings)
    {
        var processes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var services = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (settings != null)
            foreach (var s in settings)
            {
                if (!string.IsNullOrEmpty(s.RestartProcess)) processes.Add(s.RestartProcess!);
                if (!string.IsNullOrEmpty(s.RestartService)) services.Add(s.RestartService!);
            }
        return (processes, services);
    }

    // Catalog-Setting equivalent: the def's separate RestartProcess / RestartService are unified into
    // ApplyBehavior.Restart (a single RestartTarget) - lossless because NO setting sets both (verified in
    // source + pinned by RestartTargetCatalogEquivalenceTests).
    internal static (HashSet<string> Processes, HashSet<string> Services) CollectRestartTargets(
        IEnumerable<Setting> settings)
    {
        var processes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var services = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (settings != null)
            foreach (var s in settings)
            {
                switch (s.Apply.Restart)
                {
                    case RestartProcess rp when !string.IsNullOrEmpty(rp.Name): processes.Add(rp.Name); break;
                    case RestartService rs when !string.IsNullOrEmpty(rs.Name): services.Add(rs.Name); break;
                }
            }
        return (processes, services);
    }

    private async Task FlushSetsAsync(HashSet<string> processes, HashSet<string> services)
    {
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
