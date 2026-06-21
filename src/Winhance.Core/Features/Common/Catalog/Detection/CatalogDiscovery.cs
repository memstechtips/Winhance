using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Resolves a setting's current state. A setting with a custom <see cref="IStateDetector"/> delegates to
/// it; otherwise each registry or scheduled-task target is read and reduced, and the detection engine matches
/// the readings to a state. Reads go through the injected context so this is testable without a real system.
/// PowerCfg targets are read by a later wiring step (design 4A: power detection is context-keyed, deferred).
/// </summary>
public static class CatalogDiscovery
{
    /// <summary>
    /// The label of the setting's current state, or null for Custom. Registry reads (and key-existence
    /// checks for ValueName-less targets) go through <paramref name="context"/>, which also serves Tier-2
    /// custom detectors.
    /// </summary>
    public static string? DetectState(Setting setting, IDetectionContext context)
    {
        if (setting.Detector is { } detector)
            return detector.Detect(setting, context);

        var readings = new DictReadings();
        foreach (var target in setting.Targets)
        {
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
            // PowerCfgTarget reads are wired in by a later step (deferred, design 4A).
        }

        return StateDetectionEngine.Detect(setting.States, readings);
    }
}
