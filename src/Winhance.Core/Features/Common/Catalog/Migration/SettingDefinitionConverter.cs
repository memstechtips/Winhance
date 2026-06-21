using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: translates an old toggle SettingDefinition into the new Setting model
/// so the equivalence harness can compare old vs new. Deleted once the migration is complete.</summary>
public static class SettingDefinitionConverter
{
    public static Setting ConvertToggle(SettingDefinition def)
    {
        // A mirror = the same ValueName written under multiple KeyPaths -> one target, many paths.
        var groups = def.RegistrySettings.GroupBy(r => r.ValueName ?? "KeyExists").ToList();
        var targets = BuildTargets(groups);

        var enabledSet = new Dictionary<string, StateValue>();
        var disabledSet = new Dictionary<string, StateValue>();
        foreach (var g in groups)
        {
            var first = g.First();
            if (first.ValueName == null)
            {
                // ValueName == null toggles encode state as key existence, not a stored value. Mirror the
                // old detection's shape rule: inverted (rare) when DisabledValue carries the null sentinel
                // and EnabledValue does not; otherwise standard, where Enabled means the key is present.
                bool inverted = first.EnabledValue?.Contains(null) != true
                                && first.DisabledValue?.Contains(null) == true;
                enabledSet[g.Key] = inverted ? StateValue.Absent : StateValue.Exists;
                disabledSet[g.Key] = inverted ? StateValue.Exists : StateValue.Absent;
                continue;
            }
            enabledSet[g.Key] = ToStateValue(first.EnabledValue) ?? StateValue.Exists;
            disabledSet[g.Key] = ToStateValue(first.DisabledValue) ?? StateValue.Absent;
        }

        var rec = SettingDefinitionToggleState.GetRecommendedToggleState(def);
        var def_ = SettingDefinitionToggleState.GetDefaultToggleState(def);

        var enabled = new SettingState { Label = "Enabled", Set = enabledSet, Roles = RolesFor(true, rec, def_) };
        var disabled = new SettingState { Label = "Disabled", Set = disabledSet, Roles = RolesFor(false, rec, def_), IsFallback = true };

        return new Setting
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description,
            GroupName = def.GroupName,
            Icon = def.Icon,
            Targets = targets.Cast<Target>().ToList(),
            States = new[] { enabled, disabled },
        };
    }

    /// <summary>Translates an old registry SELECTION (ComboBox) SettingDefinition into the new Setting model.
    /// One state per ComboBox option (Label = option DisplayName); each option's ValueMappings become the
    /// state's per-target accept Set; IsRecommended/IsDefault become roles; the IsDefault option becomes the
    /// catch-all fallback when the definition opts in via ResolveUnmatchedToDefault.</summary>
    public static Setting ConvertSelection(SettingDefinition def)
    {
        var groups = def.RegistrySettings.GroupBy(r => r.ValueName ?? "KeyExists").ToList();
        var targets = BuildTargets(groups);

        // Old ResolveRawValuesToIndex substitutes a target's DefaultValue when the live read is absent, so a
        // mapping value equal to that DefaultValue must also accept absence. Index DefaultValue by group key.
        var defaultByKey = groups.ToDictionary(g => g.Key, g => g.First().DefaultValue);

        // Old ResolveRawValuesToIndex resolves an all-targets-absent read to the IsDefault option (its "all
        // backing absent" path) even when ResolveUnmatchedToDefault is off. For a single-target selection
        // that collapses to "the one value is absent", so let that option's value also accept absence. Scoped
        // to single-target to avoid a multi-key option matching a partially-absent read (which old would not).
        bool singleTarget = groups.Count == 1;

        var options = def.ComboBox!.Options;
        var states = new List<SettingState>(options.Count);
        foreach (var opt in options)
        {
            var set = new Dictionary<string, StateValue>();
            if (opt.ValueMappings is { } vm)
            {
                bool absorbAbsent = opt.IsDefault && singleTarget && !def.ResolveUnmatchedToDefault;
                foreach (var (key, expected) in vm)
                {
                    var sv = ToSelectionStateValue(expected, defaultByKey.TryGetValue(key, out var dv) ? dv : null);
                    if (absorbAbsent)
                        sv = sv.OrAbsent();
                    set[key] = sv;
                }
            }

            var roles = new List<StateRole>();
            if (opt.IsRecommended) roles.Add(new StateRole(RoleKind.Recommended));
            if (opt.IsDefault) roles.Add(new StateRole(RoleKind.WindowsDefault));

            states.Add(new SettingState
            {
                Label = opt.DisplayName,
                Set = set,
                Roles = roles,
                // ResolveUnmatchedToDefault: an unrecognised live state resolves to the IsDefault option.
                IsFallback = def.ResolveUnmatchedToDefault && opt.IsDefault,
            });
        }

        return new Setting
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description,
            GroupName = def.GroupName,
            Icon = def.Icon,
            Targets = targets.Cast<Target>().ToList(),
            States = states,
        };
    }

    /// <summary>Folds registry settings into targets: a mirror (same ValueName under several KeyPaths) is one
    /// target with many paths. ValueName == null groups under the "KeyExists" key (key-existence target).</summary>
    private static List<RegTarget> BuildTargets(List<IGrouping<string, RegistrySetting>> groups) =>
        groups.Select(g =>
        {
            var first = g.First();
            return new RegTarget(
                g.Key,
                g.Select(r => r.KeyPath).ToArray(),
                first.ValueName,
                first.ValueType)
            {
                ByteIndex = first.BinaryByteIndex,
                BitMask = first.BitMask,
                ByteOnly = first.ModifyByteOnly,
                CompositeStringKey = first.CompositeStringKey,
                PerNetworkInterface = first.ApplyPerNetworkInterface,
                PerMonitor = first.ApplyPerMonitor,
                IsGroupPolicy = first.IsGroupPolicy,
                LockKeyAccess = first.LockKeyAccess,
            };
        }).ToList();

    /// <summary>Maps one ComboBox ValueMapping scalar to the accept-value for a selection state. A null
    /// mapping means the value is absent; a concrete value matches that value when present, and also accepts
    /// absence when it equals the target's DefaultValue (old detection reads absent as the DefaultValue).</summary>
    private static StateValue ToSelectionStateValue(object? expected, object? defaultValue)
    {
        if (expected is null)
            return StateValue.Absent;
        var sv = StateValue.Of(expected);
        if (defaultValue is not null && CatalogValueComparer.AreEqual(defaultValue, expected))
            sv = sv.OrAbsent();
        return sv;
    }

    private static IReadOnlyList<StateRole> RolesFor(bool isEnabledState, bool? recommendedIsEnabled, bool? defaultIsEnabled)
    {
        var roles = new List<StateRole>();
        if (recommendedIsEnabled is bool r && r == isEnabledState) roles.Add(new StateRole(RoleKind.Recommended));
        if (defaultIsEnabled is bool d && d == isEnabledState) roles.Add(new StateRole(RoleKind.WindowsDefault));
        return roles;
    }

    private static StateValue? ToStateValue(object?[]? arr)
    {
        if (arr is null) return null;
        var nonNull = arr.Where(v => v != null).ToArray();
        bool hasNull = arr.Any(v => v == null);
        if (nonNull.Length == 0) return StateValue.Absent;            // [null]
        if (nonNull.Length == 1) return hasNull ? StateValue.Of(nonNull[0]!).OrAbsent() : StateValue.Of(nonNull[0]!);
        return StateValue.OneOf(nonNull);                              // 2+ concrete values
    }
}
