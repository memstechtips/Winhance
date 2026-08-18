using System.Net.NetworkInformation;
using System.Net.Sockets;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>The live detection context: the real Windows reads behind <see cref="IDetectionContext"/>, so the
/// detection engine can read a machine. Registry, DNS, build and system-restore reads delegate straight through;
/// the asynchronous reads (scheduled tasks, powercfg values, the active power plan) are pre-fetched per batch by
/// <see cref="PrefetchAsync"/> and then served synchronously from a cache, keeping the engine and detectors
/// synchronous. One instance per detection batch - it holds that batch's pre-fetch cache.</summary>
public sealed class SystemDetectionContext : IPrefetchableDetectionContext
{
    private readonly IWindowsRegistryService _reg;
    private readonly ISystemRestoreService _restore;
    private readonly IScheduledTaskStateService _tasks;
    private readonly IPowerSettingsQueryService _power;
    private readonly ILogService _log;

    private Dictionary<string, bool?> _taskCache = new();
    private Dictionary<string, (int? acValue, int? dcValue)> _powerCache = new();
    private bool _powerPrefetched;
    private string? _activePlanGuid;
    private string? _activePlanName;
    private bool _planPrefetched;
    private IReadOnlyList<DynamicOption> _installedPlans = System.Array.Empty<DynamicOption>();

    public SystemDetectionContext(
        IWindowsRegistryService reg,
        ISystemRestoreService restore,
        IScheduledTaskStateService tasks,
        IPowerSettingsQueryService power,
        ILogService log)
    {
        _reg = reg;
        _restore = restore;
        _tasks = tasks;
        _power = power;
        _log = log;
    }

    public WinBuild CurrentBuild
    {
        get
        {
            const string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            int build = int.TryParse(_reg.GetValue(key, "CurrentBuildNumber")?.ToString(), out var b) ? b : 0;
            int ubr = int.TryParse(_reg.GetValue(key, "UBR")?.ToString(), out var r) ? r : 0;
            return new WinBuild(build, ubr);
        }
    }

    public object? GetValue(string keyPath, string? valueName) => _reg.GetValue(keyPath, valueName ?? "");

    public Microsoft.Win32.RegistryValueKind? GetValueKind(string keyPath, string? valueName) =>
        _reg.GetValueKind(keyPath, valueName ?? "");

    public string[] GetSubKeyNames(string keyPath) => _reg.GetSubKeyNames(keyPath);

    public bool KeyExists(string keyPath) => _reg.KeyExists(keyPath);

    public string? PrimaryDnsV4OfActiveAdapter()
    {
        var activeAdapter = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
        if (activeAdapter == null)
            return null;

        // DNS via DHCP leaves NameServer empty; that reads as the Automatic state.
        var nameServer = _reg.GetValue(
            $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{activeAdapter.Id}",
            "NameServer") as string;
        if (string.IsNullOrEmpty(nameServer))
            return null;

        var primaryDns = activeAdapter.GetIPProperties().DnsAddresses
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?
            .ToString();
        return string.IsNullOrEmpty(primaryDns) ? null : primaryDns;
    }

    public bool IsSystemRestoreEnabled() => _restore.IsEnabledForC();

    /// <summary>Mirrors UpdateService.AreCriticalDllsRenamed: the update-policy Disabled state is enforced by renaming
    /// the two critical DLLs to "_BAK" backups, so it reads as "a _BAK backup exists AND the live DLL is gone". A thin
    /// direct filesystem read (like the network/registry reads above) - the throwaway/test contexts inherit the false
    /// default from IDetectionContext, so only this live context touches disk.</summary>
    public bool CriticalUpdateDllsRenamed()
    {
        foreach (var dll in new[] { "WaaSMedicSvc.dll", "wuaueng.dll" })
        {
            var dllPath = $@"C:\Windows\System32\{dll}";
            var backupPath = $@"C:\Windows\System32\{System.IO.Path.GetFileNameWithoutExtension(dll)}_BAK.dll";
            if (System.IO.File.Exists(backupPath) && !System.IO.File.Exists(dllPath))
                return true;
        }
        return false;
    }

    public bool? ScheduledTaskEnabled(string taskPath)
    {
        if (_taskCache.TryGetValue(taskPath, out var enabled))
            return enabled;

        _log.Log(LogLevel.Warning,
            $"[SystemDetectionContext] Scheduled task '{taskPath}' was not pre-fetched; returning null.");
        return null;
    }

    public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context)
    {
        if (!_powerPrefetched)
        {
            _log.Log(LogLevel.Warning,
                $"[SystemDetectionContext] Power setting '{settingGuid}' read before a powercfg pre-fetch; returning null.");
            return null;
        }

        // A setting absent from the active scheme's read set reads as not present.
        if (!_powerCache.TryGetValue(settingGuid, out var values))
            return null;

        return context == PowerContext.DC ? values.dcValue : values.acValue;
    }

    public string? ActivePowerPlanGuid()
    {
        if (!_planPrefetched)
            _log.Log(LogLevel.Warning,
                "[SystemDetectionContext] Active power plan read before a pre-fetch; returning null.");
        return _activePlanGuid;
    }

    public string? ActivePowerPlanName()
    {
        if (!_planPrefetched)
            _log.Log(LogLevel.Warning,
                "[SystemDetectionContext] Active power plan name read before a pre-fetch; returning null.");
        return _activePlanName;
    }

    public IReadOnlyList<DynamicOption> InstalledPowerPlans()
    {
        if (!_planPrefetched)
            _log.Log(LogLevel.Warning,
                "[SystemDetectionContext] Installed power plans read before a pre-fetch; returning none.");
        return _installedPlans;
    }

    public async Task PrefetchAsync(IReadOnlyCollection<Setting> settings)
    {
        var build = CurrentBuild;

        // One connection for every task path: a per-path read opens its own out-of-process Schedule.Service
        // instance, so a page navigation would activate N at once. Cache holds enabled / disabled / absent.
        var taskPaths = settings
            .SelectMany(s => LiveTargets(s, build).OfType<TaskTarget>())
            .Select(t => t.TaskPath)
            .Distinct()
            .ToList();
        if (taskPaths.Count > 0)
        {
            var read = await Task.Run(() => _tasks.GetTasksEnabled(taskPaths)).ConfigureAwait(false);
            _taskCache = new Dictionary<string, bool?>(read, StringComparer.OrdinalIgnoreCase);
        }

        // PowerCfg: one batched read of the active scheme's AC/DC values, keyed by setting GUID. PowerCfgValue
        // serves it via the same key (PowerCfgTarget.SettingGuid).
        bool needsPower = settings.Any(s => LiveTargets(s, build).OfType<PowerCfgTarget>().Any());
        if (needsPower)
        {
            _powerCache = await _power.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT").ConfigureAwait(false);
            _powerPrefetched = true;
        }

        // Active power plan + the installed plans (the runtime-sourced options): read once when a setting selects
        // the power plan. Both the active GUID and each option's GUID are lowercased so a dynamic-option setting can
        // match the current selection to an option Value directly (no index round-trip).
        bool needsPlan = settings.Any(s => s.Detector is PowerPlanDetector || s.OptionSource is PowerPlanOptionSource);
        if (needsPlan)
        {
            var plan = await _power.GetActivePowerPlanAsync().ConfigureAwait(false);
            _activePlanGuid = string.IsNullOrEmpty(plan?.Guid) ? null : plan.Guid.ToLowerInvariant();

            var plans = await _power.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            // The dropdown list (predefined plans incl. not-installed, localized labels, custom plans, sorted)
            // in GUID-valued shape.
            _installedPlans = PowerPlanOptions.Build(plans);
            // The active plan's RAW OS name (availablePlans.FirstOrDefault(IsActive).Name).
            _activePlanName = plans.FirstOrDefault(p => p.IsActive)?.Name;

            _planPrefetched = true;
        }
    }

    private static IEnumerable<Target> LiveTargets(Setting setting, WinBuild build) =>
        setting.Targets.Where(t => t.AppliesTo.Count == 0 || t.AppliesTo.Any(r => r.Contains(build)));
}
