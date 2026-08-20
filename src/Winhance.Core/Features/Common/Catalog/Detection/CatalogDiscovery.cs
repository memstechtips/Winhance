namespace Winhance.Core.Features.Common.Catalog;

// Only targets live on the context's current build are read (Target.AppliesTo); PowerCfg targets are read by a later wiring step.
public static class CatalogDiscovery
{
    public static SettingDetection Detect(Setting setting, IDetectionContext context, PowerContext powerContext = PowerContext.AC)
    {
        if (setting.Detector is { } detector)
            return SettingDetection.FromLabel(detector.Detect(setting, context));

        var readings = new DictReadings();
        var activeKeys = new HashSet<string>();
        var regReadTargets = new List<RegTarget>(); // active, read-authoritative registry targets, in order
        bool allRegistry = true;
        foreach (var target in setting.Targets)
        {
            if (target.AppliesTo.Count > 0 && !target.AppliesTo.Any(r => r.Contains(context.CurrentBuild)))
                continue; // target not live on this build

            activeKeys.Add(target.Key);

            if (target is RegTarget reg)
            {
                var reading = RegTargetReader.Read(reg, context);

                // A value stored under a type its target cannot reduce is malformed, not "some state we
                // don't recognize". Short-circuit BEFORE either matcher runs: this covers the
                // single-value precedence path and the whole-pattern path with one check, and keeps both
                // matchers pure. ApplyOnly targets are excluded - they are written but never read, so
                // their stored type does not affect what the user sees.
                if (reading.KindMismatch && !reg.ApplyOnly)
                    return SettingDetection.Malformed(DescribeKindMismatch(reg, context));

                readings.Set(reg.Key, reading.Value, reading.Present);
                if (!reg.ApplyOnly)
                    regReadTargets.Add(reg);
            }
            else if (target is TaskTarget task)
            {
                // A scheduled task reads as its enabled flag; an absent task (null) reads as not present.
                bool? enabled = context.ScheduledTaskEnabled(task.TaskPath);
                readings.Set(task.Key, enabled, present: enabled.HasValue);
                allRegistry = false;
            }
            else if (target is PowerCfgTarget power)
            {
                int? value = context.PowerCfgValue(power.SubgroupGuid, power.SettingGuid, powerContext);
                readings.Set(power.Key, value, present: value.HasValue);
                allRegistry = false;
            }
        }

        // Registry-only settings resolve the way Windows does: the highest-precedence target that is present
        // decides the state (a group-policy override outranks the preference key; mirror/sync keys flagged
        // ApplyOnly are written but not read). This applies ONLY to the "single authoritative value" shape -
        // exactly one read target that is not a group-policy override. Two or more independent preference keys
        // are AND-semantics (each discriminates a different state, e.g. apps-theme AND system-theme) and keep
        // whole-pattern matching, as do all non-registry mechanisms.
        bool precedenceShaped = regReadTargets.Count(t => !t.IsGroupPolicy) == 1;
        if (allRegistry && regReadTargets.Count > 0 && precedenceShaped)
            return SettingDetection.FromLabel(DetectByPrecedence(setting.States, readings, regReadTargets));

        return SettingDetection.FromLabel(StateDetectionEngine.Detect(setting.States, readings, activeKeys));
    }

    // Only runs on the malformed path, so the extra re-read costs nothing in the normal case.
    private static string DescribeKindMismatch(RegTarget reg, IDetectionContext context)
    {
        foreach (var path in RegTargetReader.OrderHklmFirst(reg.Paths))
        {
            var raw = context.GetValue(path, reg.ValueName);
            if (raw is null)
                continue;

            string actual = context.GetValueKind(path, reg.ValueName)?.ToString() ?? raw.GetType().Name;
            return $"'{reg.ValueName}' under '{path}' is stored as {actual} but the catalog expects {reg.Type}";
        }

        return $"'{reg.ValueName}' is stored under a type the catalog ({reg.Type}) cannot read";
    }

    // Precedence: a present group-policy target wins, else the first present target, else the first target. A PRESENT
    // value Winhance does not recognize reports Custom (null); an ABSENT deciding value falls to the IsFallback state.
    private static string? DetectByPrecedence(
        IReadOnlyList<SettingState> states, IStateReadings readings, IReadOnlyList<RegTarget> regTargets)
    {
        bool Present(string key)
        {
            readings.TryGet(key, out _, out var present);
            return present;
        }

        RegTarget deciding =
            regTargets.FirstOrDefault(t => t.IsGroupPolicy && Present(t.Key))
            ?? regTargets.FirstOrDefault(t => Present(t.Key))
            ?? regTargets[0];

        SettingState? fallback = null;
        foreach (var state in states)
        {
            if (state.IsFallback)
                fallback = state;
            if (state.Set.TryGetValue(deciding.Key, out var expected))
            {
                readings.TryGet(deciding.Key, out var current, out var present);
                if (expected.Matches(current, present))
                    return state.Label;
            }
        }

        // Nothing matched. A present deciding value that no state recognizes is genuinely Custom (null);
        // an absent deciding value still falls to the IsFallback state.
        return Present(deciding.Key) ? null : fallback?.Label;
    }

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
