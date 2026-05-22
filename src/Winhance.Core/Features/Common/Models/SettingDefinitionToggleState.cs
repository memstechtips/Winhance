using System;
using System.Linq;
using Microsoft.Win32;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Single source of truth for resolving the Recommended / Default toggle state of a
/// Toggle/CheckBox setting. Per-card buttons (SettingItemViewModel) and bulk Quick Actions
/// (BulkSettingsActionService) both call into here so the two paths can never disagree.
///
/// Algorithm:
///   1. Explicit <see cref="SettingDefinition.RecommendedToggleState"/> /
///      <see cref="SettingDefinition.DefaultToggleState"/> override wins.
///   2. Otherwise, evaluate the primary RegistrySetting (first IsPrimary, else first overall):
///      - Map DefaultValue / RecommendedValue to a toggle state by checking which of
///        EnabledValue / DisabledValue contains it.
///      - For Default only, when the target value is null, the null sentinel inside
///        EnabledValue / DisabledValue expresses the "key absent = Windows default" convention
///        and is treated as a match. Recommended deliberately does NOT do this — the explicit
///        override field exists for that case.
///      - Key-existence toggles (no ValueName, no Enabled/Disabled arrays, RegistryValueKind.None)
///        default to true (Windows default = key present).
///   3. Otherwise, fall back to the first ScheduledTaskSetting whose Default/Recommended state is set.
///   4. Otherwise null — caller skips (no badge / no quick-set action).
/// </summary>
public static class SettingDefinitionToggleState
{
    public static RegistrySetting? GetPrimaryRegistrySetting(SettingDefinition setting) =>
        setting.RegistrySettings?.FirstOrDefault(r => r.IsPrimary)
        ?? setting.RegistrySettings?.FirstOrDefault();

    public static bool? GetRecommendedToggleState(SettingDefinition setting)
    {
        if (setting.RecommendedToggleState is bool explicitState) return explicitState;

        var reg = GetPrimaryRegistrySetting(setting);
        if (reg != null)
        {
            var fromReg = ResolveToggleStateInternal(reg, reg.RecommendedValue, deriveFromKeyAbsent: false);
            if (fromReg is bool b) return b;
        }

        var taskSetting = setting.ScheduledTaskSettings?.FirstOrDefault(ts => ts.RecommendedState.HasValue);
        return taskSetting?.RecommendedState;
    }

    public static bool? GetDefaultToggleState(SettingDefinition setting)
    {
        if (setting.DefaultToggleState is bool explicitState) return explicitState;

        var reg = GetPrimaryRegistrySetting(setting);
        if (reg != null)
        {
            var fromReg = ResolveToggleStateInternal(reg, reg.DefaultValue, deriveFromKeyAbsent: true);
            if (fromReg is bool b) return b;
        }

        var taskSetting = setting.ScheduledTaskSettings?.FirstOrDefault(ts => ts.DefaultState.HasValue);
        return taskSetting?.DefaultState;
    }

    public static bool IsKeyExistenceToggle(RegistrySetting r) =>
        r.ValueName == null
        && r.EnabledValue == null
        && r.DisabledValue == null
        && r.ValueType == RegistryValueKind.None;

    public static bool? ToggleTargetState(object? targetValue, object?[]? enabledValue, object?[]? disabledValue)
    {
        if (targetValue == null)
        {
            if (ArrayContainsNull(enabledValue)) return true;
            if (ArrayContainsNull(disabledValue)) return false;
            return null;
        }
        if (IsValueInArray(targetValue, enabledValue)) return true;
        if (IsValueInArray(targetValue, disabledValue)) return false;
        return null;
    }

    private static bool? ResolveToggleStateInternal(RegistrySetting reg, object? targetValue, bool deriveFromKeyAbsent)
    {
        if (targetValue == null && !deriveFromKeyAbsent) return null;
        if (targetValue == null && deriveFromKeyAbsent && IsKeyExistenceToggle(reg)) return true;
        return ToggleTargetState(targetValue, reg.EnabledValue, reg.DisabledValue);
    }

    private static bool ArrayContainsNull(object?[]? array) => array?.Any(v => v == null) == true;

    private static bool IsValueInArray(object value, object?[]? array)
    {
        if (array == null) return false;
        return array.Any(v => ValuesEqual(value, v));
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (Equals(a, b)) return true;
        try
        {
            var aVal = Convert.ToInt64(a);
            var bVal = Convert.ToInt64(b);
            return aVal == bVal;
        }
        catch
        {
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
