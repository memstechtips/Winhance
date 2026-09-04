using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

// Keep a setting unless it validates existence AND has powercfg targets whose GUIDs are all absent (after
// attempting to unhide via the EnablementKey), or a checked target is hardware-controlled. The enablement write
// is the constant "Attributes"=0; that VALUE is not modelled on PowerCfgTarget, so it is hardcoded here
// (CatalogPowerExistenceFilterConformanceTests pins name/type and gates the decisions).
internal sealed class CatalogPowerExistenceFilter : ICatalogPowerExistenceFilter
{
    private const string Scheme = "SCHEME_CURRENT";
    private readonly IPowerSettingsQueryService _query;
    private readonly IWindowsRegistryService _registry;
    private readonly IScheduledTaskStateService _tasks;
    private readonly ILogService _log;

    public CatalogPowerExistenceFilter(IPowerSettingsQueryService query, IWindowsRegistryService registry, IScheduledTaskStateService tasks, ILogService log)
    {
        _query = query;
        _registry = registry;
        _tasks = tasks;
        _log = log;
    }

    public async Task<IReadOnlyList<Setting>> FilterAsync(IReadOnlyList<Setting> settings)
    {
        var bulk = await _query.GetAllPowerSettingsACDCAsync(Scheme).ConfigureAwait(false);
        if (bulk.Count == 0)
            _log.Log(LogLevel.Warning, "Could not get bulk power settings; powercfg existence checks are skipped");

        // One connection for every task path in the catalog. GetTasksEnabled activates an out-of-process COM
        // server per call, and 17 settings carry task targets, so a per-setting read opened 17 of them.
        var allTaskPaths = settings
            .SelectMany(s => s.Targets.OfType<TaskTarget>())
            .Select(t => t.TaskPath)
            .Distinct()
            .ToList();
        var taskStates = allTaskPaths.Count > 0
            ? new Dictionary<string, bool?>(await Task.Run(() => _tasks.GetTasksEnabled(allTaskPaths)).ConfigureAwait(false), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);

        var result = new List<Setting>();
        foreach (var setting in settings)
        {
            var targets = setting.Targets.OfType<PowerCfgTarget>().ToList();
            var taskTargets = setting.Targets.OfType<TaskTarget>().ToList();
            if (!setting.Availability.ValidatesExistence || (targets.Count == 0 && taskTargets.Count == 0))
            {
                result.Add(setting);
                continue;
            }

            // When the bulk powercfg query failed, powercfg existence is inconclusive - keep those
            // settings rather than hiding them on a probe failure. Task existence is independent.
            var hasValid = targets.Count > 0 && bulk.Count == 0;
            foreach (var t in targets)
            {
                if (hasValid) break;
                if (bulk.ContainsKey(t.SettingGuid)) { hasValid = true; break; }

                if (t.EnablementKey is { } ek && ek.ValueName is { } valueName)
                {
                    _log.Log(LogLevel.Info, $"Attempting to enable hidden power setting: {t.SettingGuid}");
                    var wrote = false;
                    foreach (var path in ek.Paths)
                        if (_registry.SetValue(path, valueName, 0, ek.Type)) wrote = true;

                    if (wrote)
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                        var updated = await _query.GetAllPowerSettingsACDCAsync(Scheme).ConfigureAwait(false);
                        if (updated.ContainsKey(t.SettingGuid)) { hasValid = true; break; }
                    }
                }
            }

            // A scheduled task exists when the OS can answer its enabled state at all (null = the
            // task is not registered on this system, e.g. removed on this build or app not installed).
            if (!hasValid && taskTargets.Count > 0)
            {
                hasValid = taskTargets.Any(t => taskStates.TryGetValue(t.TaskPath, out var state) && state is not null);
            }

            if (!hasValid)
                continue;

            var hardwareControlled = false;
            foreach (var t in targets.Where(x => x.CheckForHardwareControl))
            {
                if (await _query.IsSettingHardwareControlledAsync(t.SubgroupGuid, t.SettingGuid).ConfigureAwait(false))
                {
                    _log.Log(LogLevel.Info, $"Filtering out hardware-controlled setting: {setting.Id} ({t.SettingGuid})");
                    hardwareControlled = true;
                    break;
                }
            }

            if (!hardwareControlled)
                result.Add(setting);
        }

        return result;
    }
}
