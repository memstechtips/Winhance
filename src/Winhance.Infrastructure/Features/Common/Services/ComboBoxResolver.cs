using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ComboBoxResolver : IComboBoxResolver
{


    /// <summary>Resolves live registry/powercfg readings to a selection option index, reading
    /// States/Targets. Guarded by ComboBoxResolverSettingConformanceTests.</summary>
    public int ResolveRawValuesToIndex(Setting setting, Dictionary<string, object?> rawValues)
    {
        // (a) DetectedIndex from a custom detector (e.g. DnsServer) - catalog-agnostic.
        if (rawValues.TryGetValue("DetectedIndex", out var detectedIndex) && detectedIndex is int di)
            return di;

        // (b) Not a selection => no enumerable options to match => 0.
        if (setting.Control != ControlKind.Selection)
            return 0;

        // (c) A resolved group-policy index short-circuits value matching.
        if (rawValues.TryGetValue("CurrentPolicyIndex", out var policyIndex))
            return policyIndex is int index ? index : 0;

        // The states ARE the ComboBox options 1:1. A selection whose options carry NO
        // ValueMappings (the DNS / system-tray detector selections, whose states have an empty Set) has no
        // value-match to run and resolves to index 0.
        var states = setting.States;
        if (states.All(s => s.Set.Count == 0))
            return 0;

        // (f) Match: the first state whose whole (non-empty) Set the readings satisfy. Each StateValue folds in
        // the target-DefaultValue substitution (StateValue.OrAbsent, authored where a mapping
        // value equals the target's DefaultValue). A state's Set key is its Target.Key, which for a
        // RegTarget IS the RawValues key (ValueName ?? "KeyExists") and for a PowerCfgTarget re-keys to "PowerCfgValue".
        for (int i = 0; i < states.Count; i++)
        {
            var set = states[i].Set;
            if (set.Count == 0)
                continue; // an option without ValueMappings never matches (the Count > 0 guard)

            bool allMatch = true;
            foreach (var entry in set)
            {
                var readKey = ReadKeyForTarget(setting, entry.Key);
                bool present = rawValues.TryGetValue(readKey, out var current) && current != null;
                if (!entry.Value.Matches(present ? current : null, present))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                return i;
        }

        // (g) No state matched: return the first state carrying the WindowsDefault role, when
        // allBackingValuesAbsent OR any state is a fallback (IsFallback).
        //
        // allBackingValuesAbsent: a key is 'absent' here only when its read is absent AND it has no
        // folded default. A key carrying an Of(x).OrAbsent() StateValue (AcceptsAbsent with a concrete accepted
        // value) on any state has a live default and is NEVER all-absent (e.g. explorer-click-items' IconUnderline
        // = Of(3).OrAbsent() keeps allBackingValuesAbsent false). The PowerCfgValue key
        // counts only when the reads carry it (a powercfg selection with no PowerCfgValue read has
        // an EMPTY backing set, so it is NOT all-absent).
        //
        // CAVEAT: a precedence-CORRECTED selection (CatalogDetectionModelConformanceTests.PrecedenceCorrectedIds - among
        // selections only gaming-touch-keyboard-service) authors an Of(x).OrAbsent() that is NOT a DefaultValue-fold
        // but a deliberate detection fix. CatalogDetectionModelConformanceTests runs each of those ids through
        // CatalogDiscovery.DetectState over CONSTRUCTED readings (clean / recommended-applied / group-policy /
        // mirror-split) and proves the corrected reading is the one Windows would show. It pins the detection MODEL
        // this fallback implements, not this method directly.
        var keysWithFoldedDefault = new HashSet<string>();
        foreach (var st in states)
            foreach (var e in st.Set)
                if (e.Value.AcceptsAbsent && e.Value.AcceptedValues.Count > 0)
                    keysWithFoldedDefault.Add(e.Key);

        bool anyBacking = false;
        bool allBackingAbsent = true;
        if (setting.Targets.OfType<PowerCfgTarget>().Any()
            && rawValues.TryGetValue("PowerCfgValue", out var pcv))
        {
            anyBacking = true;
            if (pcv != null) allBackingAbsent = false;
        }
        foreach (var rt in setting.Targets.OfType<RegTarget>())
        {
            anyBacking = true;
            bool present = rawValues.TryGetValue(rt.Key, out var rv) && rv != null;
            if (present || keysWithFoldedDefault.Contains(rt.Key)) allBackingAbsent = false;
        }
        bool allBackingValuesAbsent = anyBacking && allBackingAbsent;

        if (allBackingValuesAbsent || states.Any(s => s.IsFallback))
            for (int i = 0; i < states.Count; i++)
                if (states[i].HasRole(RoleKind.WindowsDefault, PowerContext.Always)
                    || states[i].HasRole(RoleKind.WindowsDefault, PowerContext.AC))
                    return i;

        // (h) Custom.
        return ComboBoxConstants.CustomStateIndex;
    }

    /// <summary>The RawValues dictionary key for a state's Set entry: a RegTarget's key already IS its
    /// <c>ValueName ?? "KeyExists"</c>; a PowerCfgTarget's "Power" key re-keys to "PowerCfgValue".</summary>
    private static string ReadKeyForTarget(Setting setting, string targetKey)
        => setting.Targets.FirstOrDefault(t => t.Key == targetKey) is PowerCfgTarget ? "PowerCfgValue" : targetKey;


}
