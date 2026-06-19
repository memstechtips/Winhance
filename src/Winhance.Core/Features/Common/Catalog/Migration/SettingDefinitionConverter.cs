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

        var targets = groups.Select(g =>
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

        var enabledSet = new Dictionary<string, StateValue>();
        var disabledSet = new Dictionary<string, StateValue>();
        foreach (var g in groups)
        {
            var first = g.First();
            // Null helper result = key-existence toggle: present means Enabled, absent means Disabled.
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
