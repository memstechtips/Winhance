using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// THE detection function (design §4.1). Returns the label of the first state whose every Set entry
/// the live readings satisfy; null = Custom. One engine for toggles (2 states) and selections (N) —
/// replaces the old bool-resolver AND the duplicated ResolveRawValuesToIndex. Pure; no service deps.
/// Tier-2 IStateDetector (custom detectors) is consulted by the CALLER before this engine, not here.
/// </summary>
public static class StateDetectionEngine
{
    public static string? Detect(IReadOnlyList<SettingState> states, IStateReadings readings)
    {
        SettingState? fallback = null;

        foreach (var state in states)
        {
            if (state.IsFallback)
                fallback = state; // remember it; only used if nothing matches

            if (state.Set.Count == 0)
                continue; // a state with no detectable Set cannot be matched declaratively (e.g. Action)

            bool allMatch = true;
            foreach (var (targetKey, expected) in state.Set)
            {
                readings.TryGet(targetKey, out var current, out var present);
                if (!expected.Matches(current, present))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                return state.Label;
        }

        // Nothing matched: resolve to the catch-all fallback state if the setting declares one
        // (replaces ResolveUnmatchedToDefault), else Custom.
        return fallback?.Label;
    }
}
