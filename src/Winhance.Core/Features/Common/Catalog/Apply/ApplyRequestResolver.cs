using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The apply-side inverse of the Phase 6.3 detection overlay: maps an OLD-model apply request
/// (a <see cref="SettingDefinition"/> + enable/value/resetToDefault) to a NEW-engine apply plan
/// (<see cref="ApplyOp"/> list) for execution by <see cref="ApplyExecutor"/>, or returns <c>null</c> when the
/// request is not (yet) representable in the new model so the caller falls back to the proven old apply. Pure.
///
/// Handles the common PLAIN cases - registry/scheduled-task toggles + check-boxes, plain registry/task selections,
/// stateless Actions, numeric powercfg sliders, and reset-to-default (Phase 6.4b) - by pairing the def with its
/// <see cref="SettingCatalog"/> Setting and running <see cref="ApplyPlanBuilder"/>. Returns null (-> old apply) for:
///   - an unpaired def (no SettingCatalog peer, e.g. the -win10 merged variants until the 6.5 alias),
///   - a reset-to-default of a setting that has no WindowsDefault state (no reset target can be derived),
///   - a NumericRange whose value is not an AC/DC display-units dictionary (the only shape the catalog produces),
///   - custom-detector / dynamic-option settings (DNS / system-tray / system-restore / power-plan),
///   - a selection whose value is not a plain option index, or whose option label has no matching authored state.
/// Every fallback keeps the setting on the old apply path, so nothing regresses.</summary>
public static class ApplyRequestResolver
{
    /// <summary>Resolve against the live <see cref="SettingCatalog"/>.</summary>
    public static IReadOnlyList<ApplyOp>? Resolve(
        SettingDefinition def, bool enable, object? value, bool resetToDefault, WinBuild? build = null)
        => Resolve(def, enable, value, resetToDefault, SettingCatalog.All, build);

    /// <summary>Resolve against an explicit catalog (testing seam).</summary>
    public static IReadOnlyList<ApplyOp>? Resolve(
        SettingDefinition def, bool enable, object? value, bool resetToDefault,
        IReadOnlyList<Setting> catalog, WinBuild? build = null)
    {
        var setting = catalog.FirstOrDefault(s => s.Id == def.Id);
        if (setting is null)
            return null; // unpaired -> old apply

        // Custom-detector / dynamic-option settings (DNS, system-tray, system-restore, power-plan) apply via paths
        // the plan builder does not reproduce (and their special handlers run earlier in the funnel anyway).
        if (setting.Detector is not null || setting.OptionSource is not null)
            return null;

        // Reset-to-default (Phase 6.4b 3A): apply the WindowsDefault-roled state with its per-target reset
        // write-overrides (ResetSet) - the [1,null] Explorer targets DELETE on reset where their normal Set writes 1.
        // Falls back to old apply when the setting has no WindowsDefault state (no reset target can be derived).
        if (resetToDefault)
        {
            var defaultLabel = setting.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault))?.Label;
            return defaultLabel is null ? null : ApplyPlanBuilder.Build(setting, defaultLabel, build, reset: true);
        }

        switch (def.InputType)
        {
            case InputType.Action:
                return ApplyPlanBuilder.BuildAction(setting);

            case InputType.Toggle:
            case InputType.CheckBox:
                return BuildForLabel(setting, enable ? "Enabled" : "Disabled", build);

            case InputType.Selection:
                if (value is int index
                    && def.ComboBox?.Options is { } options
                    && index >= 0 && index < options.Count)
                {
                    return BuildForLabel(setting, options[index].DisplayName, build);
                }
                return null; // non-index selection value (AC/DC dict, string, tuple) -> old apply

            case InputType.NumericRange:
                // Powercfg slider (Phase 6.4b). The funnel always passes display-units AC/DC in a dictionary
                // (UI quick-set, the recommended applier, and the config-import bridge all build that shape).
                // Pull the two values and hand them to BuildPowerCfgNumeric, which converts display->system per
                // Numeric.Units - identical to the old PowerCfgApplier.ConvertToSystemUnits. The DC battery-gate
                // is enforced downstream in the writer, so emit both contexts and keep this pure. Any other value
                // shape (or a def the new model did not author as a Numeric) falls back to the proven old apply.
                if (setting.Numeric is not null
                    && value is Dictionary<string, object?> numericDict
                    && numericDict.TryGetValue("ACValue", out var acRaw)
                    && numericDict.TryGetValue("DCValue", out var dcRaw)
                    && TryToInt(acRaw) is { } ac
                    && TryToInt(dcRaw) is { } dc)
                {
                    return ApplyPlanBuilder.BuildPowerCfgNumeric(setting, new[]
                    {
                        new ContextValue(PowerContext.AC, ac),
                        new ContextValue(PowerContext.DC, dc),
                    });
                }
                return null;

            default: // anything else -> old apply
                return null;
        }
    }

    /// <summary>Best-effort coercion of a JSON-sourced numeric (the value may box as long/double) to int;
    /// null when the input is not numeric, so the caller falls back rather than throwing.</summary>
    private static int? TryToInt(object? value)
    {
        if (value is null) return null;
        try { return Convert.ToInt32(value); }
        catch { return null; }
    }

    /// <summary>Build the plan for a named state, or null when the setting has no such state (e.g. a custom-detector
    /// selection whose option label is not an authored state) so the caller falls back rather than the builder
    /// throwing.</summary>
    private static IReadOnlyList<ApplyOp>? BuildForLabel(Setting setting, string label, WinBuild? build)
    {
        if (!setting.States.Any(s => s.Label == label))
            return null;
        return ApplyPlanBuilder.Build(setting, label, build);
    }
}
