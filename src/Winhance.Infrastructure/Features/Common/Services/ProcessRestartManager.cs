using System.Diagnostics;
using System.ServiceProcess;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ProcessRestartManager(
    IWindowsUIManagementService uiManagementService,
    IConfigImportState configImportState,
    IPendingRestartService pendingRestartService,
    IExplorerRestartService explorerRestartService,
    ILogService logService) : IProcessRestartManager
{
    /// <summary>The one restart target that is never executed here - see <see cref="HandleRestartsAsync"/>.</summary>
    private const string ExplorerTarget = "Explorer";

    /// <summary>Past this, a broadcast stops being a Debug detail and becomes a WARNING in the user's
    /// own log. A silent two-second stall is what started this; a slow broadcast has to announce
    /// itself.</summary>
    private const int SlowBroadcastMs = 500;

    private int _suppressCount;

    /// <summary>
    /// Test seam: the most recent backgrounded broadcast. Production never reads it - the broadcast is
    /// fire-and-forget and observes its own failures internally (see <see cref="RunBroadcast"/>) - but
    /// the suite needs a deterministic point to await instead of racing the thread pool.
    /// </summary>
    internal Task LastBroadcastTask { get; private set; } = Task.CompletedTask;

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

    /// <inheritdoc />
    public Task HandleProcessAndServiceRestartsAsync(Setting setting)
    {
        // The Setting's ApplyBehavior.Restart unifies process/service restarts into one RestartTarget (no
        // setting sets both). Reuse the CollectRestartTargets extraction (0/1 process, 0/1 service), then run
        // the shared restart logic.
        var (processes, services) = CollectRestartTargets(new[] { setting });

        // Which broadcast this setting deserves is DECLARED BY THE SETTING (ApplyBehavior.NotifyWindows),
        // next to its confirmation gate and its restart. This used to be inferred here from the registry
        // paths the setting happens to write - the same fact, but reverse-engineered rather than stated.
        return HandleRestartsAsync(processes.FirstOrDefault(), services.FirstOrDefault(), setting.Id,
            WantsAppearanceNotice(setting));
    }

    // Shared restart logic (process/service handled independently), parameterised by the extracted
    // (process, service, id).
    //
    // Explorer is deliberately NOT restarted here. Applying a setting used to kill the shell, so a user
    // toggling several Explorer tweaks in a row triggered several kills within seconds - overlapping
    // restart cycles, plus winlogon's AutoRestartShell giving up after repeated shell deaths, could
    // leave them with no shell at all. Instead we broadcast (so anything that CAN take effect live
    // does) and register the setting; the user restarts once, when they choose, from the
    // pending-restart bar.
    private Task HandleRestartsAsync(string? restartProcess, string? restartService, string settingId,
        bool themeAffecting)
    {
        bool isExplorer = !string.IsNullOrEmpty(restartProcess)
            && restartProcess.Equals(ExplorerTarget, StringComparison.OrdinalIgnoreCase);

        // Two INDEPENDENT reasons to tell Windows something changed:
        //   NotifyWindows - the setting DECLARES a notice (the theme settings; no restart needed), and
        //   isExplorer    - a restart-carrying setting gets the generic notice so anything that CAN take
        //                   effect live does so before the user gets round to restarting.
        // Gating the broadcast on isExplorer alone is what silently stopped the theme applying the moment
        // its (unnecessary) Explorer restart was removed: the notice was reachable only via the restart.
        if (themeAffecting || isExplorer)
        {
            // The broadcast kills nothing, so it runs even under a suppress scope or during an import -
            // it is what makes live-updatable settings apply immediately.
            //
            // It is NOT free, though, and believing it was is what hid a two-second stall with nothing
            // in the log: the theme half sends WM_SETTINGCHANGE("ImmersiveColorSet") synchronously, and
            // SendMessageTimeout charges its timeout PER TOP-LEVEL WINDOW. Two things fix that here. A
            // setting only gets the theme half if it can actually change the theme, and the whole
            // broadcast runs OFF the caller's thread - which for an interactive apply is the UI thread.
            // Backgrounding is safe at THIS call site specifically: nothing reads a result, nothing is
            // killed afterwards so there is no ordering hazard, and the HGlobal the theme send allocates
            // lives and dies entirely inside the call.

            // The dispatch line is logged HERE, on the caller's thread, so it lands in the log where
            // the user expects it even though the send itself goes to the thread pool - and so a
            // broadcast that never returns at all still leaves a trace saying it started.
            LogBroadcastDispatch(themeAffecting, settingId);
            LastBroadcastTask = Task.Run(() => RunBroadcast(themeAffecting, settingId));
        }

        // Only a restart-carrying setting raises the pending bar. A setting that merely declares a
        // notice has already taken effect and must not ask the user to restart anything.
        if (isExplorer)
        {
            RegisterExplorerPending(settingId);
            return Task.CompletedTask;
        }

        if (_suppressCount > 0)
        {
            if (!string.IsNullOrEmpty(restartProcess))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{restartProcess}' (restarts suppressed - parent will restart)");
            if (!string.IsNullOrEmpty(restartService))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping service restart for '{restartService}' (restarts suppressed - parent will restart)");
            return Task.CompletedTask;
        }

        if (configImportState.IsActive)
        {
            if (!string.IsNullOrEmpty(restartProcess))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping process restart for '{restartProcess}' (config import mode - will restart at end)");
            if (!string.IsNullOrEmpty(restartService))
                logService.Log(LogLevel.Debug, $"[ProcessRestartManager] Skipping service restart for '{restartService}' (config import mode - will restart at end)");
            return Task.CompletedTask;
        }

        if (!string.IsNullOrEmpty(restartProcess))
            RestartProcessByName(restartProcess, settingId);

        if (!string.IsNullOrEmpty(restartService))
            RestartServiceByName(restartService, settingId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers a setting as needing an Explorer restart - unless a config import is running, which
    /// performs its own single restart at the end and must therefore leave no pending bar behind.
    /// </summary>
    private void RegisterExplorerPending(string? settingId)
    {
        if (configImportState.IsActive)
        {
            logService.Log(LogLevel.Debug,
                $"[ProcessRestartManager] Broadcast Explorer-refresh for '{settingId}' (config import restarts at the end)");
            return;
        }

        if (!string.IsNullOrEmpty(settingId))
            pendingRestartService.Register(settingId);
    }

    /// <summary>"theme+generic" or "generic" - which set of messages went out, so the split is visible
    /// in the field rather than only in the source.</summary>
    private static string BroadcastVariant(bool themeAffecting) =>
        themeAffecting ? "theme+generic" : "generic";

    /// <summary>What the broadcast was for: a named setting, or a coalesced bulk apply.</summary>
    private static string BroadcastScope(string? settingId) =>
        string.IsNullOrEmpty(settingId) ? "(coalesced)" : $"for '{settingId}'";

    /// <summary>Announces a broadcast BEFORE it is sent. Separate from <see cref="RunBroadcast"/> so the
    /// per-apply path can log it on the caller's thread while the send itself runs on the pool.</summary>
    private void LogBroadcastDispatch(bool themeAffecting, string? settingId) =>
        logService.Log(LogLevel.Debug,
            $"[ProcessRestartManager] Broadcasting shell refresh ({BroadcastVariant(themeAffecting)}) {BroadcastScope(settingId)}");

    /// <summary>
    /// Sends the shell broadcast and TIMES it. Every Explorer-restart setting gets the generic,
    /// payload-free WM_SETTINGCHANGE; only a theme-affecting setting also gets the expensive theme set.
    ///
    /// The timing is the point of this method. A user reading their own log used to see a two-second
    /// gap with nothing in it and no way to attribute it. Normal runs stay Debug detail; anything at or
    /// past <see cref="SlowBroadcastMs"/> is promoted to a Warning so the slow case reports itself.
    ///
    /// Catches EVERYTHING. On the per-apply path this runs on a background task nobody awaits, so an
    /// escaping exception would be swallowed by the thread pool and never reach the log - which is the
    /// exact class of silence this change exists to remove.
    /// </summary>
    private void RunBroadcast(bool themeAffecting, string? settingId)
    {
        string variant = BroadcastVariant(themeAffecting);
        string scope = BroadcastScope(settingId);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (themeAffecting)
                explorerRestartService.BroadcastThemeRefresh();

            explorerRestartService.BroadcastShellRefresh();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logService.Log(LogLevel.Error,
                $"[ProcessRestartManager] Shell broadcast ({variant}) {scope} failed after {stopwatch.ElapsedMilliseconds}ms",
                ex);
            return;
        }

        stopwatch.Stop();
        long elapsedMs = stopwatch.ElapsedMilliseconds;
        bool slow = elapsedMs >= SlowBroadcastMs;
        logService.Log(
            slow ? LogLevel.Warning : LogLevel.Debug,
            slow
                ? $"[ProcessRestartManager] Shell broadcast ({variant}) {scope} took {elapsedMs}ms - other top-level windows are slow to process it"
                : $"[ProcessRestartManager] Shell broadcast ({variant}) {scope} took {elapsedMs}ms");
    }

    /// <inheritdoc />
    public Task FlushCoalescedRestartsAsync(IEnumerable<Setting> appliedSettings)
    {
        if (appliedSettings == null) return Task.CompletedTask;

        var settings = appliedSettings.Where(s => s != null).ToList();
        var (processes, services) = CollectRestartTargets(settings);

        // Explorer never restarts as part of a bulk apply either - the whole point of deferring is that
        // the USER decides when the shell goes down. Register every Explorer-carrying setting and drop
        // the target from the flush set. Skipping this would quietly reintroduce the kill through every
        // bulk path (apply-recommended, reset-section), which is the bug this change exists to remove.
        if (processes.Remove(ExplorerTarget))
        {
            // ONE broadcast for the whole batch, and it carries the theme set only when at least one of
            // the applied settings could actually change the theme. Synchronous here on purpose: FlushSets
            // below kills processes, so unlike the per-apply path this one has an ordering relationship
            // with what follows it.
            bool batchAffectsTheme = settings.Any(WantsAppearanceNotice);
            LogBroadcastDispatch(batchAffectsTheme, settingId: null);
            RunBroadcast(batchAffectsTheme, settingId: null);
            foreach (var setting in settings.Where(HasExplorerRestart))
                RegisterExplorerPending(setting.Id);
        }

        FlushSets(processes, services);
        return Task.CompletedTask;
    }

    /// <summary>
    /// True when the setting DECLARES that applying it changes how Windows looks
    /// (<see cref="WindowsChange.Appearance"/>) - the one thing the expensive half of the broadcast is for.
    ///
    /// Reading the declaration is ALL this does. It does not decide whether a broadcast happens at all -
    /// the Explorer gate in <see cref="HandleRestartsAsync"/> still does that - only which one goes out.
    /// </summary>
    private static bool WantsAppearanceNotice(Setting setting) =>
        setting.Apply.NotifyWindows.HasFlag(WindowsChange.Appearance);

    /// <summary>True when a setting's unified ApplyBehavior.Restart is a process restart of Explorer.</summary>
    private static bool HasExplorerRestart(Setting setting) =>
        setting.Apply.Restart is RestartProcess rp
        && rp.Name.Equals(ExplorerTarget, StringComparison.OrdinalIgnoreCase);

    // Reads process / service restarts from ApplyBehavior.Restart (a single RestartTarget) - no setting
    // sets both.
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

    private void FlushSets(HashSet<string> processes, HashSet<string> services)
    {
        if (processes.Count == 0 && services.Count == 0) return;

        logService.Log(LogLevel.Info,
            $"[ProcessRestartManager] Flushing coalesced restarts: {processes.Count} process(es), {services.Count} service(s)");

        foreach (var process in processes)
            RestartProcessByName(process, settingIdForLog: null);

        foreach (var service in services)
            RestartServiceByName(service, settingIdForLog: null);
    }

    // Explorer never reaches here - both entry points intercept it before this point and register it as
    // pending instead. Only intl (a broadcast) and genuine process kills remain.
    private void RestartProcessByName(string processName, string? settingIdForLog)
    {
        if (processName.Equals("intl", StringComparison.OrdinalIgnoreCase))
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
            if (serviceName.Contains('*'))
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
