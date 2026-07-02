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
        string settingId, bool enable, object? value, bool resetToDefault, WinBuild? build = null)
        => Resolve(settingId, enable, value, resetToDefault, SettingCatalog.All, build);

    /// <summary>Resolve against an explicit catalog (testing seam).</summary>
    public static IReadOnlyList<ApplyOp>? Resolve(
        string settingId, bool enable, object? value, bool resetToDefault,
        IReadOnlyList<Setting> catalog, WinBuild? build = null)
    {
        var setting = catalog.FirstOrDefault(s => s.Id == settingId);
        if (setting is null)
            return null; // unpaired -> old apply

        // Dynamic-option settings (power-plan): the selected value is the scheme GUID - a plain string (the live UI
        // selection, Slice 7b-ui-3a) or a {Guid,Name} dictionary (config import, ConfigurationApplicationBridgeService).
        // Build the activate op directly from that GUID; the setting has no States, so ApplyPlanBuilder/BuildForLabel
        // cannot be used. A non-GUID value (a legacy int index, which needs an async index->GUID lookup the pure
        // resolver can't do, or null) is not representable here -> old apply. ADDITIVE until the Slice 8b-2 flip: while
        // the PowerService special handler is still registered the funnel catches power-plan FIRST and never reaches
        // this seam, so this branch is dead until that registration is removed.
        if (setting.OptionSource is not null)
        {
            var guid = ExtractPowerPlanGuid(value);
            return guid is null ? null : new ApplyOp[] { new PowerPlanActivateOp(guid) };
        }

        // A BARE-state custom detector (states carry NO apply effects) has nothing for the new engine to build, so it
        // falls back to the old apply. A custom-detector setting WHOSE states carry apply effects (system-tray /
        // system-restore / DNS, Slice 5/6) is allowed through and flows into the reset block / InputType switch.
        if (setting.Detector is not null && !setting.States.Any(s => s.Effects.Count > 0))
            return null;

        // Custom-detector RESET now routes through the general reset block below: an effects-based detector with a
        // WindowsDefault state (system-restore) resolves to Build(WindowsDefault, reset:true) - proven equivalent to
        // the old executor reset by CustomDetectorResetApplyEquivalenceTests. A detector with no WindowsDefault state
        // (system-tray / DNS selections) hits the null return there and stays on the old apply, unchanged.
        // (updates-policy-mode is the one custom detector with a WindowsDefault state AND registry targets, but it is
        // special-handled - see SettingServicesExtensions - so it never reaches Resolve; if that registration is ever
        // removed, its reset must get its own Build(reset:true) equivalence proof before relying on this path.)

        // Reset-to-default (Phase 6.4b 3A): apply the WindowsDefault-roled state with its per-target reset
        // write-overrides (ResetSet) - the [1,null] Explorer targets DELETE on reset where their normal Set writes 1.
        // Falls back to old apply when the setting has no WindowsDefault state (no reset target can be derived).
        if (resetToDefault)
        {
            var defaultLabel = setting.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault))?.Label;
            return defaultLabel is null ? null : ApplyPlanBuilder.Build(setting, defaultLabel, build, reset: true);
        }

        // Render-kind drives the apply shape: the catalog Setting's derived Control (proven == the old
        // InputType by ControlDerivationConformanceTests). CheckBox folds into Toggle.
        switch (setting.Control)
        {
            case ControlKind.Action:
                return ApplyPlanBuilder.BuildAction(setting);

            case ControlKind.Toggle:
                return BuildForLabel(setting, enable ? "Enabled" : "Disabled", build);

            case ControlKind.Selection:
                // The selection index maps to the state at that position (States[i].Label == the old
                // ComboBox.Options[i].DisplayName by construction - the converter built States from Options).
                if (value is int index
                    && index >= 0 && index < setting.States.Count)
                {
                    return BuildForLabel(setting, setting.States[index].Label, build);
                }
                // Separate AC/DC powercfg selection (config-import (acIndex,dcIndex) tuple / UI {ACValue,DCValue} index
                // dict): the old PowerCfgApplier wrote GetValueFromIndex(acIndex) -> AC and GetValueFromIndex(dcIndex)
                // -> DC (asymmetric), which Build(stateLabel) cannot express (it writes one option to BOTH contexts).
                // Route to the dedicated AC/DC builder ONLY when every target is a SEPARATE (AC/DC) PowerCfgTarget and
                // both indices are in range. A powercfg selection's enablement registry is nested INSIDE the
                // PowerCfgTarget (EnablementKey) and applied out-of-band by the existence phase, NOT by this AC/DC
                // write - so a normal enablement-bearing powercfg selection (power-button-action etc.) IS pure here and
                // routes correctly (proven by the AC/DC equivalence harness). The All(is PowerCfgTarget { Separate })
                // guard keeps a hypothetical future sibling-registry or non-Separate powercfg selection on the old apply
                // (BuildPowerCfgSelectionAcDc writes only the powercfg target, and the old applier's AC/DC path is
                // itself gated on Separate); every other shape (registry CustomStateValues dict, string) falls back too.
                if (setting.Targets.Count > 0
                    && setting.Targets.All(t => t is PowerCfgTarget { Mode: PowerModeSupport.Separate })
                    && TryReadAcDcIndices(value) is { } acdc
                    && acdc.Ac >= 0 && acdc.Ac < setting.States.Count
                    && acdc.Dc >= 0 && acdc.Dc < setting.States.Count)
                {
                    return ApplyPlanBuilder.BuildPowerCfgSelectionAcDc(setting, acdc.Ac, acdc.Dc);
                }

                return null; // non-index / non-AC-DC selection value (registry CustomStateValues, string) -> old apply

            case ControlKind.Slider:
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

    /// <summary>Reads a separate-AC/DC selection value into (acIndex, dcIndex): a config-import (int,int) tuple or a
    /// {ACValue, DCValue} index dictionary (the UI AC/DC quick-set). Returns null for any other shape (a registry
    /// CustomStateValues dict has no ACValue/DCValue keys, a display-name string, etc.) so the caller falls back.</summary>
    private static (int Ac, int Dc)? TryReadAcDcIndices(object? value)
    {
        switch (value)
        {
            case ValueTuple<int, int> tuple:
                return (tuple.Item1, tuple.Item2);
            case Dictionary<string, object?> dict
                when dict.TryGetValue("ACValue", out var ac) && dict.TryGetValue("DCValue", out var dc)
                    && TryToInt(ac) is { } acIndex && TryToInt(dc) is { } dcIndex:
                return (acIndex, dcIndex);
            default:
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

    /// <summary>Extract the power scheme GUID from a dynamic-option apply value: a plain GUID string (the live UI
    /// selection) or a {Guid,Name} dictionary (config import). Returns null when no usable GUID is present (a legacy
    /// int index or null) so the caller falls back to the old apply rather than building a bogus op.</summary>
    private static string? ExtractPowerPlanGuid(object? value)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        if (value is Dictionary<string, object> dict
            && dict.TryGetValue("Guid", out var g)
            && g?.ToString() is { Length: > 0 } guid)
            return guid;
        return null;
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
