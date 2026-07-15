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


    /// <summary>Catalog-<see cref="Setting"/> mirror of
    /// <c>ResolveRawValuesToIndex(SettingDefinition, Dictionary{string, object?})</c>: resolves live
    /// registry/powercfg readings to a selection option index reading the NEW model (States/Targets) instead of
    /// the def's ComboBox/RegistrySettings. Equivalence-proven == the def overload over the whole selection
    /// population by ComboBoxResolverSettingEquivalenceTests. Additive; wired to nothing until the reader cutover.</summary>
    public int ResolveRawValuesToIndex(Setting setting, Dictionary<string, object?> rawValues)
    {
        // (a) DetectedIndex from a custom detector (e.g. DnsServer) - catalog-agnostic, identical to the def method.
        if (rawValues.TryGetValue("DetectedIndex", out var detectedIndex) && detectedIndex is int di)
            return di;

        // (b) Not a selection => no enumerable options to match => 0 (the def's `ComboBox?.Options == null`).
        if (setting.Control != ControlKind.Selection)
            return 0;

        // (c) A resolved group-policy index short-circuits value matching - identical to the def method.
        if (rawValues.TryGetValue("CurrentPolicyIndex", out var policyIndex))
            return policyIndex is int index ? index : 0;

        // The states ARE the ComboBox options 1:1 (F1-proven order). A selection whose options carry NO
        // ValueMappings (the DNS / system-tray detector selections, whose states have an empty Set) has no
        // value-match to run and resolves to index 0 - the def's `mappings == null => return 0`.
        var states = setting.States;
        if (states.All(s => s.Set.Count == 0))
            return 0;

        // (f) Match: the first state whose whole (non-empty) Set the readings satisfy. Each StateValue folds in
        // the old target-DefaultValue substitution (StateValue.OrAbsent, authored by the converter where a mapping
        // value equals the target's DefaultValue), reproducing the def's absent->DefaultValue fill without a
        // standalone DefaultValue (the catalog model dropped it). A state's Set key is its Target.Key, which for a
        // RegTarget IS the old RawValues key (ValueName ?? "KeyExists") and for a PowerCfgTarget re-keys to "PowerCfgValue".
        for (int i = 0; i < states.Count; i++)
        {
            var set = states[i].Set;
            if (set.Count == 0)
                continue; // an option without ValueMappings never matched in the def loop (the Count > 0 guard)

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

        // (g) No state matched: reproduce the def's two fallbacks, BOTH of which return the first IsDefault
        // (WindowsDefault-role) option - the def's `(allBackingValuesAbsent || ResolveUnmatchedToDefault) ->
        // first IsDefault option`. ResolveUnmatchedToDefault is the catalog IsFallback state.
        //
        // allBackingValuesAbsent mirrors the def's `currentValues.Count > 0 && all null`, where each currentValue
        // is the live read OR (when absent) the target's DefaultValue. So a key is 'absent' here only when its read
        // is absent AND it has no target DefaultValue. The catalog dropped the standalone DefaultValue but folds it
        // into an Of(x).OrAbsent() StateValue (AcceptsAbsent with a concrete accepted value), so a key carrying one
        // on any state has a live default and is NEVER all-absent (matching e.g. explorer-click-items' IconUnderline
        // = Of(3).OrAbsent(), whose def DefaultValue 3 keeps allBackingValuesAbsent false). The PowerCfgValue key
        // counts only when the reads carry it (the def's guard: a powercfg selection with no PowerCfgValue read has
        // an EMPTY backing set, so it is NOT all-absent).
        //
        // CAVEAT: a precedence-CORRECTED selection (CatalogDetectionModelConformanceTests.PrecedenceCorrectedIds - among
        // selections only gaming-touch-keyboard-service) authors an Of(x).OrAbsent() that is NOT a DefaultValue-fold
        // but a deliberate detection fix (its def DefaultValue is null). This overload then diverges from the def
        // there BY DESIGN: the old .Any/DefaultValue detection is exactly the bug the catalog retires. That divergence
        // is CORRECT, not merely tolerated: CatalogDetectionModelConformanceTests runs each of those ids through
        // CatalogDiscovery.DetectState over CONSTRUCTED readings (clean / recommended-applied / group-policy /
        // mirror-split) and proves the corrected reading is the one Windows would show. It pins the detection MODEL
        // this fallback implements, not this method directly.
        // HISTORICAL: until the SettingDefinition teardown, ComboBoxResolverSettingEquivalenceTests additionally
        // proved the divergence set was EXACTLY those ids. That oracle died with the def -- with no old model left
        // there is nothing to diverge FROM, so the set is no longer bounded by a test; the id list it bounded now
        // lives on CatalogDetectionModelConformanceTests.
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

    /// <summary>The old RawValues dictionary key for a state's Set entry: a RegTarget's key already IS its
    /// <c>ValueName ?? "KeyExists"</c>; a PowerCfgTarget's "Power" key re-keys to "PowerCfgValue".</summary>
    private static string ReadKeyForTarget(Setting setting, string targetKey)
        => setting.Targets.FirstOrDefault(t => t.Key == targetKey) is PowerCfgTarget ? "PowerCfgValue" : targetKey;


}
