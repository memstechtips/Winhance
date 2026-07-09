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
    private static Dictionary<int, Dictionary<string, object?>>? ValueMappingsView(ComboBoxMetadata? meta)
    {
        if (meta?.Options is null) return null;
        var dict = new Dictionary<int, Dictionary<string, object?>>();
        for (int i = 0; i < meta.Options.Count; i++)
        {
            if (meta.Options[i].ValueMappings is { } vm)
                dict[i] = vm;
        }
        return dict.Count == 0 ? null : dict;
    }

    private static string[]? DisplayNamesView(ComboBoxMetadata? meta)
        => meta?.Options?.Select(o => o.DisplayName).ToArray();

    public int GetValueFromIndex(SettingDefinition setting, int index)
    {
        if (index == ComboBoxConstants.CustomStateIndex)
        {
            return 0;
        }

        if (setting.ComboBox?.Options == null)
        {
            return index;
        }

        var mappings = ValueMappingsView(setting.ComboBox);
        if (mappings != null && mappings.TryGetValue(index, out var valueDict))
        {
            var firstValue = valueDict.Values.FirstOrDefault();
            return firstValue is int intVal ? intVal : (firstValue != null ? Convert.ToInt32(firstValue) : index);
        }

        return index;
    }



    public int ResolveRawValuesToIndex(SettingDefinition setting, Dictionary<string, object?> rawValues)
    {
        // Handle DetectedIndex from custom detection (e.g., DnsServer)
        if (rawValues.TryGetValue("DetectedIndex", out var detectedIndex) && detectedIndex is int di)
            return di;

        if (setting.ComboBox?.Options == null)
        {
            return 0;
        }

        if (rawValues.TryGetValue("CurrentPolicyIndex", out var policyIndex))
        {
            return policyIndex is int index ? index : 0;
        }

        var mappings = ValueMappingsView(setting.ComboBox);
        if (mappings == null) return 0;
        var currentValues = new Dictionary<string, object?>();

        if (setting.PowerCfgSettings?.Count > 0 && rawValues.TryGetValue("PowerCfgValue", out var powerCfgValue))
        {
            currentValues["PowerCfgValue"] = powerCfgValue != null ? Convert.ToInt32(powerCfgValue) : null;
        }

        foreach (var registrySetting in setting.RegistrySettings)
        {
            var key = registrySetting.ValueName ?? "KeyExists";
            if (rawValues.TryGetValue(key, out var rawValue) && rawValue != null)
            {
                currentValues[key] = rawValue;
            }
            else if (registrySetting.DefaultValue != null)
            {
                currentValues[key] = registrySetting.DefaultValue;
            }
            else
            {
                currentValues[key] = null;
            }
        }

        foreach (var mapping in mappings)
        {
            var index = mapping.Key;
            var expectedValues = mapping.Value;

            bool allMatch = true;
            foreach (var expectedValue in expectedValues)
            {
                if (!currentValues.TryGetValue(expectedValue.Key, out var currentValue))
                {
                    currentValue = null;
                }

                if (!ValuesAreEqual(currentValue, expectedValue.Value))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch && expectedValues.Count > 0)
            {
                return index;
            }
        }

        // No option matched. Fall back to the IsDefault option when either:
        //  - every backing registry value is absent (a pristine system is the Windows default), or
        //  - the setting opts in via ResolveUnmatchedToDefault (its default state isn't a single
        //    enumerable value, so any unrecognised state is treated as the default).
        bool allBackingValuesAbsent = currentValues.Count > 0 && currentValues.Values.All(v => v is null);
        if ((allBackingValuesAbsent || setting.ResolveUnmatchedToDefault)
            && setting.ComboBox?.Options is { } defaultOptions)
        {
            for (int i = 0; i < defaultOptions.Count; i++)
            {
                if (defaultOptions[i].IsDefault)
                {
                    return i;
                }
            }
        }

        return ComboBoxConstants.CustomStateIndex;
    }

    /// <summary>Catalog-<see cref="Setting"/> mirror of
    /// <see cref="ResolveRawValuesToIndex(SettingDefinition, Dictionary{string, object?})"/>: resolves live
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
        // CAVEAT: a precedence-CORRECTED selection (CatalogAuthoringEquivalenceTests.PrecedenceCorrectedIds - among
        // selections only gaming-touch-keyboard-service) authors an Of(x).OrAbsent() that is NOT a DefaultValue-fold
        // but a deliberate detection fix (its def DefaultValue is null). This overload then diverges from the def
        // there BY DESIGN: the old .Any/DefaultValue detection is exactly the bug the catalog retires. That divergence
        // is accepted (and bounded to those ids) by ComboBoxResolverSettingEquivalenceTests, mirroring how
        // CatalogAuthoringEquivalenceTests excludes the same ids from its 1:1 gate.
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


    public Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index)
    {
        var result = new Dictionary<string, object?>();

        if (setting.ComboBox?.Options == null)
        {
            return result;
        }

        var mappings = ValueMappingsView(setting.ComboBox);
        if (mappings != null && mappings.TryGetValue(index, out var expectedValues))
        {
            foreach (var expectedValue in expectedValues)
            {
                result[expectedValue.Key] = expectedValue.Value;
            }
        }

        return result;
    }

    public int GetIndexFromDisplayName(SettingDefinition setting, string displayName)
    {
        if (DisplayNamesView(setting.ComboBox) is { } displayNames)
        {
            for (int i = 0; i < displayNames.Length; i++)
            {
                if (string.Equals(displayNames[i], displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return 0;
    }

    private static bool ValuesAreEqual(object? value1, object? value2)
        => Utilities.ValueComparer.ValuesAreEqual(value1, value2);
}
