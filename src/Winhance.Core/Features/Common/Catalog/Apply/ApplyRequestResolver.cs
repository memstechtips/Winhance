using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Maps a setting-apply request (a string <c>settingId</c> + enable/value/resetToDefault) to an apply
/// plan (<see cref="ApplyOp"/> list) for execution by <see cref="ApplyExecutor"/>. Total for every reachable
/// production request shape (proven by <c>ResolveTotalityAuditTests</c>); returns <c>null</c> only for request
/// shapes that never reach it in production - the setting is special-handled before Resolve, or the shape is one
/// the catalog never produces. When Resolve does return null the caller fails loudly (a logged unaudited-request-
/// shape failure), not a fallback. Pure.
///
/// Handles the PLAIN cases - registry/scheduled-task toggles + check-boxes, plain registry/task selections,
/// stateless Actions, numeric powercfg sliders, and reset-to-default - by pairing the settingId with its
/// <see cref="SettingCatalog"/> Setting and running <see cref="ApplyPlanBuilder"/>. Returns null (all cases below
/// unreachable in production per ResolveTotalityAuditTests) for:
///   - an unpaired settingId (no SettingCatalog peer; a retired -win10 id normalizes to its canonical merged peer),
///   - a reset-to-default of a stateless Action (no default state to apply; other no-WindowsDefault resets fall through),
///   - a NumericRange whose value is not an AC/DC display-units dictionary (the only shape the catalog produces),
///   - a bare-state custom-detector setting (its states carry no apply effects),
///   - a selection whose value is not a plain option index, or whose option label has no matching authored state.</summary>
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
        // Alias-normalize so a retired "-win10" This PC id resolves to its canonical MERGED catalog Setting (the 6
        // build-gated merges); Normalize is identity for every other id. Config import already normalizes
        // before apply (ConfigMigrationService); this covers the live UI apply, which passes the setting's id.
        var setting = catalog.FirstOrDefault(s => s.Id == SettingIdAliases.Normalize(settingId));
        if (setting is null)
            return null; // unpaired (no catalog peer)

        // Dynamic-option settings (power-plan): the selected value is the scheme GUID - a plain string (the live UI
        // selection) or a {Guid,Name} dictionary (config import, ConfigurationApplicationBridgeService).
        // Build the activate op directly from that GUID; the setting has no States, so ApplyPlanBuilder/BuildForLabel
        // cannot be used. A non-GUID value (a legacy int index, which needs an async index->GUID lookup the pure
        // resolver can't do, or null) is not representable here and returns null (unreachable in production).
        if (setting.OptionSource is not null)
        {
            var guid = ExtractPowerPlanGuid(value);
            return guid is null ? null : new ApplyOp[] { new PowerPlanActivateOp(guid) };
        }

        // A BARE-state custom detector (states carry NO apply effects) has nothing to build, so it returns null
        // (unreachable in production). A custom-detector setting WHOSE states carry apply effects (system-tray /
        // system-restore / DNS) is allowed through and flows into the reset block / Control switch.
        if (setting.Detector is not null && !setting.States.Any(s => s.Effects.Count > 0))
            return null;

        // Custom-detector RESET routes through the general reset block below: an effects-based detector with a
        // WindowsDefault state (system-restore, and system-tray / DNS whose IsRecommended/IsDefault options carry
        // StateRoles) resolves to Build(WindowsDefault, reset:true). A detector with no WindowsDefault
        // state hits the null return there.
        // (updates-policy-mode is the one custom detector with a WindowsDefault state AND registry targets, but it is
        // special-handled - see SettingServicesExtensions - so it never reaches Resolve; if that registration is ever
        // removed, its reset must get its own Build(reset:true) equivalence proof before relying on this path.)

        // Reset-to-default. A reset differs from a normal apply ONLY in the per-target ResetSet overrides, which
        // live on a WindowsDefault-roled state (the [1,null] Explorer targets DELETE on reset where their normal Set
        // writes 1). So the reset resolution is:
        //   - a setting WITH a WindowsDefault state -> apply that state with reset:true, so its ResetSet kicks in.
        //   - a setting WITHOUT one declares no reset-specific overrides, so its reset IS a normal apply of the
        //     requested default value (the reset dispatcher passes the setting's default). Fall through to the
        //     normal resolution below with reset:true threaded (a no-op today - none of this population carries a
        //     ResetSet - but honest + future-proof).
        //   - a stateless Action has no default STATE to apply (BuildAction runs its one-shot effects, which is NOT
        //     a reset), so a reset is not representable and returns null.
        if (resetToDefault)
        {
            // Build-aware: a merged setting's WindowsDefault role can be OS-divergent (This PC folders default to
            // shown on Win10, hidden on Win11), so resolve the default state for the LIVE build. With no build
            // (build is null - a caller that does not thread it), only an UNCONDITIONAL WindowsDefault matches, so a
            // build-scoped default is invisible and the request falls through.
            var defaultLabel = (build is { } b
                ? setting.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault, b))
                : setting.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault)))?.Label;
            if (defaultLabel is not null)
                return ApplyPlanBuilder.Build(setting, defaultLabel, build, reset: true);
            if (setting.Control == ControlKind.Action)
                return null; // stateless action: reset not representable
            // else: fall through to the normal apply resolution below (reset:true is threaded via resetToDefault).
        }

        // Render-kind drives the apply shape: the catalog Setting's derived Control. CheckBox folds into Toggle.
        switch (setting.Control)
        {
            case ControlKind.Action:
                return ApplyPlanBuilder.BuildAction(setting);

            case ControlKind.Toggle:
                return BuildForLabel(setting, enable ? "Enabled" : "Disabled", build, resetToDefault);

            case ControlKind.Selection:
                // The selection index maps to the state at that position (States[i].Label is the option label
                // at position i).
                if (value is int index
                    && index >= 0 && index < setting.States.Count)
                {
                    return BuildForLabel(setting, setting.States[index].Label, build, resetToDefault);
                }
                // Separate AC/DC powercfg selection (config-import (acIndex,dcIndex) tuple / UI {ACValue,DCValue} index
                // dict): the AC/DC path writes GetValueFromIndex(acIndex) -> AC and GetValueFromIndex(dcIndex)
                // -> DC (asymmetric), which Build(stateLabel) cannot express (it writes one option to BOTH contexts).
                // Route to the dedicated AC/DC builder ONLY when every target is a SEPARATE (AC/DC) PowerCfgTarget and
                // both indices are in range. A powercfg selection's enablement registry is nested INSIDE the
                // PowerCfgTarget (EnablementKey) and applied out-of-band by the existence phase, NOT by this AC/DC
                // write - so a normal enablement-bearing powercfg selection (power-button-action etc.) IS pure here and
                // routes correctly. The All(is PowerCfgTarget { Separate }) guard returns null for a hypothetical
                // future sibling-registry or non-Separate powercfg selection (BuildPowerCfgSelectionAcDc writes only
                // the powercfg target, and the AC/DC write path is itself gated on Separate); every other shape
                // (registry CustomStateValues dict, string) returns null too.
                if (setting.Targets.Count > 0
                    && setting.Targets.All(t => t is PowerCfgTarget { Mode: PowerModeSupport.Separate })
                    && TryReadAcDcIndices(value) is { } acdc
                    && acdc.Ac >= 0 && acdc.Ac < setting.States.Count
                    && acdc.Dc >= 0 && acdc.Dc < setting.States.Count)
                {
                    return ApplyPlanBuilder.BuildPowerCfgSelectionAcDc(setting, acdc.Ac, acdc.Dc);
                }

                // Registry-selection CUSTOM state (config-import CustomStateValues, a Dictionary<string,object> of raw
                // per-ValueName values for a "Custom"/no-option state). Route to BuildRegistryCustomState for a PLAIN
                // registry selection - every target a plain-value RegTarget (no per-NIC/monitor/composite, with a
                // ValueName). This applies the raw per-ValueName registry values ONLY: a per-option PowerShell script
                // is NOT run on a Custom-state import, because a "Custom" state is not one of the named options
                // (Marco 2026-07-03 - the old executor ran an arbitrary enable-direction script here, which was
                // incoherent for a no-option state). The normal option apply (int index) still runs the option's
                // script via the state's Effects; only this raw custom-state re-apply is registry-only.
                if (value is Dictionary<string, object> customValues
                    && setting.Targets.Count > 0
                    && setting.Targets.All(t => t is RegTarget
                        { PerNetworkInterface: false, PerMonitor: false, CompositeStringKey: null, ValueName: not null })
                    // A Dictionary<string,object?> AC/DC dict is the SAME runtime type as Dictionary<string,object>
                    // (nullable annotations are erased), so require the dict to actually carry one of this
                    // setting's RegTarget ValueNames - an AC/DC dict (ACValue/DCValue keys) does not, and returns null.
                    && setting.Targets.OfType<RegTarget>().Any(r => customValues.ContainsKey(r.ValueName ?? "KeyExists")))
                {
                    return ApplyPlanBuilder.BuildRegistryCustomState(setting, customValues);
                }

                return null; // remaining non-index selection value (string display-name)

            case ControlKind.Slider:
                // Powercfg slider. The funnel always passes display-units AC/DC in a dictionary
                // (UI quick-set, the recommended applier, and the config-import bridge all build that shape).
                // Pull the two values and hand them to BuildPowerCfgNumeric, which converts display->system per
                // Numeric.Units. The DC battery-gate is enforced downstream in the writer, so emit both contexts
                // and keep this pure. Any other value shape (or a setting not authored as a Numeric) returns null.
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

            default: // anything else
                return null;
        }
    }

    /// <summary>Reads a separate-AC/DC selection value into (acIndex, dcIndex): a config-import (int,int) tuple or a
    /// {ACValue, DCValue} index dictionary (the UI AC/DC quick-set). Returns null for any other shape (a registry
    /// CustomStateValues dict has no ACValue/DCValue keys, a display-name string, etc.).</summary>
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
    /// null when the input is not numeric, so the caller returns null rather than throwing.</summary>
    private static int? TryToInt(object? value)
    {
        if (value is null) return null;
        try { return Convert.ToInt32(value); }
        catch { return null; }
    }

    /// <summary>Extract the power scheme GUID from a dynamic-option apply value: a plain GUID string (the live UI
    /// selection) or a {Guid,Name} dictionary (config import). Returns null when no usable GUID is present (a legacy
    /// int index or null) so the caller returns null rather than building a bogus op.</summary>
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
    /// selection whose option label is not an authored state) so the caller returns null rather than the builder
    /// throwing.</summary>
    private static IReadOnlyList<ApplyOp>? BuildForLabel(Setting setting, string label, WinBuild? build, bool reset = false)
    {
        if (!setting.States.Any(s => s.Label == label))
            return null;
        return ApplyPlanBuilder.Build(setting, label, build, reset);
    }
}
