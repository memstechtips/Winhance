using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Resolves a setting's current state. A setting with a custom <see cref="IStateDetector"/> delegates to
/// it; otherwise each registry or scheduled-task target is read and reduced, and the detection engine matches
/// the readings to a state. Reads go through the injected context so this is testable without a real system.
/// Only targets live on the context's current build are read (Target.AppliesTo). PowerCfg targets are read by
/// a later wiring step (power detection is context-keyed, deferred).
/// </summary>
public static class CatalogDiscovery
{
    /// <summary>
    /// The label of the setting's current state, or null for Custom. Registry reads (and key-existence
    /// checks for ValueName-less targets) go through <paramref name="context"/>, which also serves Tier-2
    /// custom detectors.
    /// </summary>
    public static string? DetectState(Setting setting, IDetectionContext context, PowerContext powerContext = PowerContext.AC)
    {
        if (setting.Detector is { } detector)
            return detector.Detect(setting, context);

        var readings = new DictReadings();
        var activeKeys = new HashSet<string>();
        foreach (var target in setting.Targets)
        {
            if (target.AppliesTo.Count > 0 && !target.AppliesTo.Any(r => r.Contains(context.CurrentBuild)))
                continue; // target not live on this build

            activeKeys.Add(target.Key);

            if (target is RegTarget reg)
            {
                var (value, present) = RegTargetReader.Read(reg, context);
                readings.Set(reg.Key, value, present);
            }
            else if (target is TaskTarget task)
            {
                // A scheduled task reads as its enabled flag; an absent task (null) reads as not present.
                bool? enabled = context.ScheduledTaskEnabled(task.TaskPath);
                readings.Set(task.Key, enabled, present: enabled.HasValue);
            }
            else if (target is PowerCfgTarget power)
            {
                int? value = context.PowerCfgValue(power.SubgroupGuid, power.SettingGuid, powerContext);
                readings.Set(power.Key, value, present: value.HasValue);
            }
        }

        return StateDetectionEngine.Detect(setting.States, readings, activeKeys);
    }

    /// <summary>The raw current value of a numeric (slider) setting for the given context, or null when not
    /// present. A slider has no enumerated states - its value IS the reading. Reads the setting's single
    /// PowerCfgTarget (numeric settings are powercfg-backed) through the context.</summary>
    public static int? DetectValue(Setting setting, IDetectionContext context, PowerContext powerContext = PowerContext.AC)
    {
        foreach (var target in setting.Targets)
        {
            if (target.AppliesTo.Count > 0 && !target.AppliesTo.Any(r => r.Contains(context.CurrentBuild)))
                continue;
            if (target is PowerCfgTarget power)
                return context.PowerCfgValue(power.SubgroupGuid, power.SettingGuid, powerContext);
        }
        return null;
    }
}
