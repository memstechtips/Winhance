using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>Existence gate for settings whose mechanism may not exist on this machine: powercfg GUIDs
/// (with unhide-via-EnablementKey) and scheduled tasks (registered or not). Originally reproduced the old
/// PowerSettingsValidationService.FilterSettingsByExistenceAsync for the new catalog model,
/// branch-for-branch: keep a setting unless it validates existence AND has powercfg targets whose GUIDs are all
/// absent (after attempting to unhide via the EnablementKey), or a checked target is hardware-controlled. The
/// enablement write is the constant "Attributes"=0 the old EnablementRegistrySetting wrote, reproduced via
/// IWindowsRegistryService.SetValue - the same service the old
/// filter used. The =0 write VALUE is not modelled on PowerCfgTarget (it carries only path/name/type), so it is
/// hardcoded here; the name/type half is pinned by CatalogPowerExistenceFilterConformanceTests, which also gates
/// this filter's decisions over constructed probes (machine-independent).</summary>
public sealed class CatalogPowerExistenceFilter : ICatalogPowerExistenceFilter
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
            _log.Log(LogLevel.Warning, "[CatalogPowerExistenceFilter] Could not get bulk power settings; powercfg existence checks are skipped");

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
                    _log.Log(LogLevel.Info, $"[CatalogPowerExistenceFilter] Attempting to enable hidden power setting: {t.SettingGuid}");
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
            // Read this setting's task targets over one Task Scheduler connection rather than opening one
            // per target; the read is off-thread because the COM call blocks.
            if (!hasValid && taskTargets.Count > 0)
            {
                var paths = taskTargets.Select(t => t.TaskPath).Distinct().ToList();
                var states = await Task.Run(() => _tasks.GetTasksEnabled(paths)).ConfigureAwait(false);
                hasValid = states.Values.Any(state => state is not null);
            }

            if (!hasValid)
                continue;

            var hardwareControlled = false;
            foreach (var t in targets.Where(x => x.CheckForHardwareControl))
            {
                if (await _query.IsSettingHardwareControlledAsync(t.SubgroupGuid, t.SettingGuid).ConfigureAwait(false))
                {
                    _log.Log(LogLevel.Info, $"[CatalogPowerExistenceFilter] Filtering out hardware-controlled setting: {setting.Id} ({t.SettingGuid})");
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
