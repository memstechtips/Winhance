using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Resolves a setting's current state. A setting with a custom <see cref="IStateDetector"/> delegates to
/// it; otherwise each registry target is read and reduced, and the detection engine matches the readings
/// to a state. Pure — the raw registry read is injected so this is testable without a registry. Non-registry
/// targets (powercfg, scheduled task) are read by the wiring layer in a later step.
/// </summary>
public static class CatalogDiscovery
{
    /// <summary>
    /// The label of the setting's current state, or null for Custom. <paramref name="rawGet"/> is
    /// (keyPath, valueName) -> raw registry value or null. <paramref name="context"/> serves Tier-2 detectors.
    /// </summary>
    public static string? DetectState(Setting setting, Func<string, string?, object?> rawGet, IDetectionContext context)
    {
        if (setting.Detector is { } detector)
            return detector.Detect(setting, context);

        var readings = new DictReadings();
        foreach (var target in setting.Targets)
        {
            if (target is RegTarget reg)
            {
                var (value, present) = RegTargetReader.Read(reg, rawGet);
                readings.Set(reg.Key, value, present);
            }
            // PowerCfgTarget / TaskTarget reads are wired in by the platform layer later.
        }

        return StateDetectionEngine.Detect(setting.States, readings);
    }
}
