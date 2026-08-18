namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// THE detection function. Returns the label of the first state whose every live Set entry the readings
/// satisfy; null = Custom. One engine for toggles (2 states) and selections (N) - replaces the old
/// bool-resolver AND the duplicated ResolveRawValuesToIndex. Pure; no service deps.
/// IStateDetector (custom detectors) is consulted by the CALLER before this engine, not here.
/// When activeTargetKeys is supplied, Set entries for targets not live on the current build are ignored.
/// </summary>
public static class StateDetectionEngine
{
    public static string? Detect(
        IReadOnlyList<SettingState> states,
        IStateReadings readings,
        IReadOnlyCollection<string>? activeTargetKeys = null)
    {
        SettingState? fallback = null;

        foreach (var state in states)
        {
            if (state.IsFallback)
                fallback = state; // remember it; only used if nothing matches

            bool anyChecked = false;
            bool allMatch = true;
            foreach (var (targetKey, expected) in state.Set)
            {
                if (activeTargetKeys != null && !activeTargetKeys.Contains(targetKey))
                    continue; // target not live on this build; ignore its Set entry
                anyChecked = true;
                readings.TryGet(targetKey, out var current, out var present);
                if (!expected.Matches(current, present))
                {
                    allMatch = false;
                    break;
                }
            }

            if (!anyChecked)
                continue; // no live, declarative Set entry (Action, or every entry build-inactive)

            if (allMatch)
                return state.Label;
        }

        // Nothing matched: resolve to the catch-all fallback state if the setting declares one
        // (replaces ResolveUnmatchedToDefault), else Custom.
        return fallback?.Label;
    }
}
