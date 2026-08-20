namespace Winhance.Core.Features.Common.Catalog;

// The label of the first state whose every live Set entry the readings satisfy; null = Custom. Custom detectors
// (IStateDetector) are consulted by the CALLER, not here. With activeTargetKeys, Set entries for targets not
// live on the current build are ignored.
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

        // Nothing matched: resolve to the catch-all fallback state if the setting declares one, else Custom.
        return fallback?.Label;
    }
}
