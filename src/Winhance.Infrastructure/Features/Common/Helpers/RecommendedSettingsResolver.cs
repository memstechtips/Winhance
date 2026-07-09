using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Helpers;

internal class OSInfo
{
    public int BuildNumber { get; set; }
    public int BuildRevision { get; set; }
    public bool IsWindows10 { get; set; }
    public bool IsWindows11 { get; set; }
}

internal static class RecommendedSettingsResolver
{
    internal static OSInfo BuildOSInfo(IWindowsVersionService versionService) =>
        new OSInfo
        {
            BuildNumber = versionService.GetWindowsBuildNumber(),
            BuildRevision = versionService.GetWindowsBuildRevision(),
            IsWindows10 = !versionService.IsWindows11(),
            IsWindows11 = versionService.IsWindows11()
        };

    internal static bool IsCompatibleWithCurrentOS(SettingDefinition setting, OSInfo osInfo)
    {
        if (setting.IsWindows10Only && !osInfo.IsWindows10) return false;
        if (setting.IsWindows11Only && !osInfo.IsWindows11) return false;

        if (setting.SupportedBuildRanges?.Count > 0)
        {
            bool inSupportedRange = setting.SupportedBuildRanges.Any(range =>
                osInfo.BuildNumber >= range.MinBuild && osInfo.BuildNumber <= range.MaxBuild);
            if (!inSupportedRange) return false;
        }
        else if (!BuildVersionGate.IsCompatible(
            osInfo.BuildNumber,
            osInfo.BuildRevision,
            setting.MinimumBuildNumber,
            setting.MinimumBuildRevision,
            setting.MaximumBuildNumber,
            setting.MaximumBuildRevision))
        {
            return false;
        }

        return true;
    }

    internal static bool HasRecommendedValue(SettingDefinition setting, WinBuild build)
    {
        var paired = SettingCatalog.Find(setting.Id);
        if (paired is not null && CatalogToggleState.GetRecommended(paired, build).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsRecommended) == true) return true;
        return false;
    }

    internal static bool HasDefaultValue(SettingDefinition setting, WinBuild build)
    {
        var paired = SettingCatalog.Find(setting.Id);
        if (paired is not null && CatalogToggleState.GetDefault(paired, build).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsDefault) == true) return true;
        return false;
    }

    internal static object? GetRecommendedValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
        return registrySetting?.RecommendedValue;
    }

    internal static object? GetDefaultValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.DefaultValue != null);
        return registrySetting?.DefaultValue;
    }

    internal static int? GetRecommendedIndex(SettingDefinition setting)
    {
        var opts = setting.ComboBox?.Options;
        if (opts is null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (opts[i].IsRecommended) return i;
        return null;
    }

    internal static int? GetDefaultIndex(SettingDefinition setting)
    {
        var opts = setting.ComboBox?.Options;
        if (opts is null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (opts[i].IsDefault) return i;
        return null;
    }

    // For PowerCfg-backed Selection/NumericRange settings, build the value shape that
    // SettingApplicationService → PowerCfgApplier expects (matches what SettingItemViewModel
    // sends for AC/DC quick-set buttons). Returns null if the setting isn't PowerCfg-backed
    // or if neither AC nor DC has a target value.
    internal static object? BuildPowerCfgApplyValue(SettingDefinition setting, bool useRecommended)
    {
        var pcfg = setting.PowerCfgSettings?.FirstOrDefault();
        if (pcfg == null) return null;

        int? acRaw = useRecommended ? pcfg.RecommendedValueAC : pcfg.DefaultValueAC;
        int? dcRaw = useRecommended ? pcfg.RecommendedValueDC : pcfg.DefaultValueDC;
        if (!acRaw.HasValue && !dcRaw.HasValue) return null;

        bool isSeparate = pcfg.PowerModeSupport == PowerModeSupport.Separate;

        if (setting.InputType == InputType.Selection)
        {
            int? acIdx = FindOptionIndexForPowerCfgValue(setting, acRaw);
            int? dcIdx = FindOptionIndexForPowerCfgValue(setting, dcRaw);

            if (isSeparate)
            {
                if (!acIdx.HasValue && !dcIdx.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acIdx ?? 0,
                    ["DCValue"] = dcIdx ?? 0
                };
            }
            return (object?)(acIdx ?? dcIdx);
        }

        if (setting.InputType == InputType.NumericRange)
        {
            // Stored values are system units (e.g. Seconds). PowerCfgApplier converts
            // display→system on its end, so we hand it display units here.
            string displayUnits = GetPowerCfgDisplayUnits(setting);
            int? acDisplay = acRaw.HasValue ? ConvertSystemToDisplayUnits(acRaw.Value, displayUnits) : null;
            int? dcDisplay = dcRaw.HasValue ? ConvertSystemToDisplayUnits(dcRaw.Value, displayUnits) : null;

            if (isSeparate)
            {
                if (!acDisplay.HasValue && !dcDisplay.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acDisplay ?? 0,
                    ["DCValue"] = dcDisplay ?? 0
                };
            }
            return (object?)(acDisplay ?? dcDisplay);
        }

        return null;
    }

    internal static int? FindOptionIndexForPowerCfgValue(SettingDefinition setting, int? targetValue)
    {
        if (!targetValue.HasValue) return null;
        var opts = setting.ComboBox?.Options;
        if (opts == null) return null;
        for (int i = 0; i < opts.Count; i++)
        {
            if (opts[i].ValueMappings is { } m && m.TryGetValue("PowerCfgValue", out var v) && v != null)
            {
                try { if (Convert.ToInt32(v) == targetValue.Value) return i; }
                catch { }
            }
        }
        return null;
    }

    internal static string GetPowerCfgDisplayUnits(SettingDefinition setting)
    {
        if (setting.NumericRange?.Units is { } unitsStr) return unitsStr;
        return setting.PowerCfgSettings?[0]?.Units ?? string.Empty;
    }

    // ---- Catalog-Setting overloads (Slice C/D foundation; additive, proven == the SettingDefinition
    // versions above by PowerCfgHelperCatalogEquivalenceTests). Wired to nothing yet - the SAS change-history
    // rendering (E2 partial-block) + the config bridge repoint onto these when the apply-cluster ports off
    // SettingDefinition; the def versions stay live until then. ----

    // Catalog equivalent of GetPowerCfgDisplayUnits(SettingDefinition): the converter sets
    // Numeric.Units = def.NumericRange?.Units ?? pcs.Units (the combined value, for a NumericRange powercfg) and
    // PowerCfgTarget.Units = pcs.Units (the fallback for a Selection powercfg, which has no Numeric) - together
    // reproducing def.NumericRange?.Units ?? def.PowerCfgSettings?[0]?.Units ?? "".
    internal static string GetPowerCfgDisplayUnits(Setting setting)
    {
        if (setting.Numeric?.Units is { } units) return units;
        return setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault()?.Units ?? string.Empty;
    }

    // Catalog equivalent of FindOptionIndexForPowerCfgValue(SettingDefinition, int?): the converter builds one
    // State per ComboBox option (in order) whose Set[PowerCfgTarget.Key, i.e. "Power"] = StateValue.Of(the option's
    // ValueMappings["PowerCfgValue"]) - so the first State whose Set["Power"] matches the raw value is the same
    // index the def version returns from Options[i].ValueMappings["PowerCfgValue"]. Mirrors the live factory's
    // SettingViewModelFactory.FindStateIndexForPowerCfgValue.
    internal static int? FindOptionIndexForPowerCfgValue(Setting setting, int? targetValue)
    {
        if (!targetValue.HasValue) return null;
        var powerKey = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault()?.Key;
        if (powerKey is null) return null;
        var states = setting.States;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Set.TryGetValue(powerKey, out var stateValue)
                && stateValue.Matches(targetValue.Value, present: true))
                return i;
        }
        return null;
    }

    // Catalog equivalent of GetRecommendedIndex(SettingDefinition): the converter builds one State per ComboBox
    // option IN ORDER and maps opt.IsRecommended -> an UNCONDITIONAL StateRole(Recommended) on that State
    // (SettingDefinitionConverter.ConvertSelection), so the first State carrying an unconditional Recommended role
    // is the same index the def returns from the first Options[i].IsRecommended. Scoped to Selection - the def
    // reads ComboBox?.Options and returns null when there is none (Toggle/Slider/Action/PowerPlan). A powercfg
    // Selection's roles are CONTEXT-scoped (AC/DC), so the unconditional HasRole(Recommended) does not match them,
    // matching the def (whose powercfg options carry no IsRecommended flag). No merged (-win10) setting is a
    // Selection (the 6 aliases are This PC toggles), so the Selection roles are unconditional and this is
    // build-invariant. Proven == the def version over the whole population by RecommendedResolverIndexCatalog
    // EquivalenceTests.
    internal static int? GetRecommendedIndex(Setting setting)
    {
        if (setting.Control != ControlKind.Selection) return null;
        for (int i = 0; i < setting.States.Count; i++)
            if (setting.States[i].HasRole(RoleKind.Recommended)) return i;
        return null;
    }

    // Catalog equivalent of GetDefaultIndex(SettingDefinition): as GetRecommendedIndex, but the WindowsDefault
    // role (converter maps opt.IsDefault -> StateRole(WindowsDefault)).
    internal static int? GetDefaultIndex(Setting setting)
    {
        if (setting.Control != ControlKind.Selection) return null;
        for (int i = 0; i < setting.States.Count; i++)
            if (setting.States[i].HasRole(RoleKind.WindowsDefault)) return i;
        return null;
    }

    // Inverse of PowerCfgApplier.ConvertToSystemUnits.
    internal static int ConvertSystemToDisplayUnits(int systemValue, string? units)
    {
        return units?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            // 1:1 — see UnitConversionHelper for the rationale.
            "milliseconds" => systemValue,
            _ => systemValue
        };
    }

    // Inverse of ConvertSystemToDisplayUnits (== the converter's ConvertSystemToDisplay): back to raw powercfg
    // system units. minutes/hours multiply; everything else (seconds, milliseconds, percent) is 1:1.
    internal static int ConvertDisplayToSystemUnits(int displayValue, string? units) => units?.ToLowerInvariant() switch
    {
        "minutes" => displayValue * 60,
        "hours" => displayValue * 3600,
        _ => displayValue,
    };

    // ---- Slice 2 catalog-Setting value/presence overloads (additive, wired to nothing yet; proven == the
    // SettingDefinition versions above over the population by RecommendedResolverValueCatalogEquivalenceTests).
    // The apply-cluster (RecommendedSettingsApplier / BulkSettingsActionService) repoints onto these at Slice 3;
    // the def versions stay live until then. IsCompatibleWithCurrentOS has NO catalog overload - it is OBVIATED:
    // the catalog settings registry gates OS membership via CatalogMembershipFilter.IsAvailable, and a by-id
    // consumer that still needs the OS check reads Setting.Availability.Allows(build) directly (the bare-model
    // 1:1 of the old IsWindows10Only / IsWindows11Only / build-range gate) - no resolver helper is needed.
    // GetRecommendedValueForSetting / GetDefaultValueForSetting get NO catalog overload: Marco resolved the fork
    // (2026-07-08) by EXCLUDING Actions from Apply-Recommended (RecommendedSettingsApplier, mirroring the Bulk
    // reset exclusion). Their only reachable non-null else-branch case was the Action start-menu-clean-11, whose
    // RecommendedValue signal ConvertAction drops (an Action's write is a marker-less RegistryWriteEffect); with
    // Actions excluded, the else-branch population is NumericRange only (all powercfg / registry-free) where both
    // helpers return null, so they are dead-in-effect and simply drop out at the Slice 3 repoint. ----

    // Catalog equivalent of HasRecommendedValue(SettingDefinition, WinBuild). The def unions three signals: a
    // recommended toggle state (it already delegates to CatalogToggleState), a powercfg recommended AC/DC value,
    // and a registry-selection IsRecommended option. Catalog homes: the toggle via the SAME build-aware
    // CatalogToggleState.GetRecommended; a powercfg slider's recommended via Numeric.Recommended; a selection's
    // recommended (registry unconditional OR powercfg context-scoped) as a Recommended-kind role on some state.
    // The role check is Selection-scoped so a merged toggle's build-scoped role can never be caught build-unaware
    // here (selections are never merged). Powercfg present-vs-matching: the def counts a recommended AC/DC value
    // even when it maps to no option, the catalog role is present only when it matched an option - equal for the
    // real population (every recommended powercfg value is a selectable option), gated by the equivalence test.
    internal static bool HasRecommendedValue(Setting setting, WinBuild build)
    {
        if (CatalogToggleState.GetRecommended(setting, build) is not null) return true;
        if (setting.Numeric is { } numeric && numeric.Recommended.Count > 0) return true;
        if (setting.Control == ControlKind.Selection
            && setting.States.Any(s => s.Roles.Any(r => r.Kind == RoleKind.Recommended)))
            return true;
        return false;
    }

    // Catalog equivalent of HasDefaultValue(SettingDefinition, WinBuild): as HasRecommendedValue but the
    // WindowsDefault role / Numeric.WindowsDefault. The toggle part is the SAME build-aware CatalogToggleState.GetDefault
    // the def hybrid already calls, so the merged (-win10) toggles - whose Windows default is OS-divergent and
    // build-scoped - agree on either OS with zero divergence.
    internal static bool HasDefaultValue(Setting setting, WinBuild build)
    {
        if (CatalogToggleState.GetDefault(setting, build) is not null) return true;
        if (setting.Numeric is { } numeric && numeric.WindowsDefault.Count > 0) return true;
        if (setting.Control == ControlKind.Selection
            && setting.States.Any(s => s.Roles.Any(r => r.Kind == RoleKind.WindowsDefault)))
            return true;
        return false;
    }

    // Catalog equivalent of BuildPowerCfgApplyValue(SettingDefinition, bool). The def reads PowerCfgSettings[0]'s
    // Recommended/Default AC/DC raw values, maps a Selection value to its option index via
    // FindOptionIndexForPowerCfgValue and a NumericRange value to display units. The catalog carries the same: a
    // powercfg Selection's recommended/default option is the state with a context-scoped role (Recommended /
    // WindowsDefault, AC / DC) - and that index equals FindOptionIndexForPowerCfgValue(RecommendedValueAC) because
    // the converter adds the role to exactly the option whose PowerCfgValue equals the per-mode value; a NumericRange's
    // Numeric.Recommended / Numeric.WindowsDefault ContextValues are already in DISPLAY units (the converter pre-applied
    // the same system->display conversion), so they are handed over directly. isSeparate comes from PowerCfgTarget.Mode.
    internal static object? BuildPowerCfgApplyValue(Setting setting, bool useRecommended)
    {
        var pcfg = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault();
        if (pcfg is null) return null;

        bool isSeparate = pcfg.Mode == PowerModeSupport.Separate;
        var roleKind = useRecommended ? RoleKind.Recommended : RoleKind.WindowsDefault;

        if (setting.Control == ControlKind.Selection)
        {
            int? acIdx = IndexOfContextRole(setting, roleKind, PowerContext.AC);
            int? dcIdx = IndexOfContextRole(setting, roleKind, PowerContext.DC);

            if (isSeparate)
            {
                if (!acIdx.HasValue && !dcIdx.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acIdx ?? 0,
                    ["DCValue"] = dcIdx ?? 0
                };
            }
            return (object?)(acIdx ?? dcIdx);
        }

        if (setting.Numeric is { } numeric)
        {
            var contextValues = useRecommended ? numeric.Recommended : numeric.WindowsDefault;
            int? acDisplay = ContextValueFor(contextValues, PowerContext.AC);
            int? dcDisplay = ContextValueFor(contextValues, PowerContext.DC);

            if (isSeparate)
            {
                if (!acDisplay.HasValue && !dcDisplay.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acDisplay ?? 0,
                    ["DCValue"] = dcDisplay ?? 0
                };
            }
            return (object?)(acDisplay ?? dcDisplay);
        }

        return null;
    }

    // Index of the first state carrying a role of the given kind in the given power context (a powercfg selection's
    // per-mode Recommended / WindowsDefault marker). Equals FindOptionIndexForPowerCfgValue(setting, the per-mode
    // value) - the converter adds the context role to exactly the option whose PowerCfgValue matches that value.
    private static int? IndexOfContextRole(Setting setting, RoleKind kind, PowerContext context)
    {
        for (int i = 0; i < setting.States.Count; i++)
            if (setting.States[i].Roles.Any(r => r.Kind == kind && r.Context == context))
                return i;
        return null;
    }

    private static int? ContextValueFor(IReadOnlyList<ContextValue> values, PowerContext context)
    {
        foreach (var v in values)
            if (v.Context == context) return v.Value;
        return null;
    }

    // ---- Slice 5 (PowerPlanActivationService port): the recommended AC/DC SYSTEM values the service writes to a
    // freshly-created power plan via PowerProf.PowerWriteAC/DCValueIndex, per powercfg setting. The def form is the
    // verbatim extraction of ApplyRecommendedSettingsToPlanAsync's inline branches (the old-logic reference); the
    // catalog form reads the same values off the Setting. Proven catalog == def over the whole Power population by
    // PowerPlanRecommendedWriteEquivalenceTests; the service uses the catalog form. ----

    /// <summary>The (subgroup, setting, AC, DC) write the old ApplyRecommendedSettingsToPlanAsync computed for a
    /// setting. Branch 1 = the first PowerCfgSetting with a recommended AC/DC value, writing the raw SYSTEM value
    /// (AC falling back to DC and vice-versa). Branch 2 = a Selection whose Recommendation.RecommendedOptionAC/DC
    /// label maps (via ComboBox option DisplayName) to an option carrying ValueMappings["PowerCfgValue"], emitted
    /// only when BOTH AC and DC resolve. Null = nothing to write. Verbatim extraction - do not "improve".</summary>
    internal static (string SubgroupGuid, string SettingGuid, int Ac, int Dc)? ComputePlanRecommendedWrite(SettingDefinition setting)
    {
        var powerCfgWithRecommended = setting.PowerCfgSettings?.FirstOrDefault(ps =>
            ps.RecommendedValueAC.HasValue || ps.RecommendedValueDC.HasValue);

        if (powerCfgWithRecommended != null)
        {
            var acValue = powerCfgWithRecommended.RecommendedValueAC ?? powerCfgWithRecommended.RecommendedValueDC ?? 0;
            var dcValue = powerCfgWithRecommended.RecommendedValueDC ?? powerCfgWithRecommended.RecommendedValueAC ?? 0;
            return (powerCfgWithRecommended.SubgroupGuid, powerCfgWithRecommended.SettingGuid, acValue, dcValue);
        }

        if (setting.InputType == InputType.Selection
            && setting.Recommendation?.RecommendedOptionAC != null
            && setting.PowerCfgSettings?.Any() == true)
        {
            var recommendedOptionAC = setting.Recommendation.RecommendedOptionAC;
            var recommendedOptionDC = setting.Recommendation.RecommendedOptionDC ?? recommendedOptionAC;
            var options = setting.ComboBox?.Options;
            if (options != null)
            {
                var indexAC = -1;
                var indexDC = -1;
                for (int oi = 0; oi < options.Count; oi++)
                {
                    if (indexAC < 0 && string.Equals(options[oi].DisplayName, recommendedOptionAC, StringComparison.Ordinal))
                        indexAC = oi;
                    if (indexDC < 0 && string.Equals(options[oi].DisplayName, recommendedOptionDC, StringComparison.Ordinal))
                        indexDC = oi;
                }

                if (options.Any(o => o.ValueMappings != null))
                {
                    int? acValue = null, dcValue = null;
                    if (indexAC >= 0 && options[indexAC].ValueMappings is { } valueDictAC
                        && valueDictAC.TryGetValue("PowerCfgValue", out var powerCfgValueAC) && powerCfgValueAC != null)
                        acValue = Convert.ToInt32(powerCfgValueAC);
                    if (indexDC >= 0 && options[indexDC].ValueMappings is { } valueDictDC
                        && valueDictDC.TryGetValue("PowerCfgValue", out var powerCfgValueDC) && powerCfgValueDC != null)
                        dcValue = Convert.ToInt32(powerCfgValueDC);

                    if (acValue.HasValue && dcValue.HasValue)
                    {
                        var pcs0 = setting.PowerCfgSettings[0];
                        return (pcs0.SubgroupGuid, pcs0.SettingGuid, acValue.Value, dcValue.Value);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Catalog equivalent of ComputePlanRecommendedWrite(SettingDefinition): the single PowerCfgTarget
    /// supplies the subgroup/setting GUIDs; the per-context recommended SYSTEM value is the Recommended-role
    /// state's Set["Power"] payload (Selection) or Numeric.Recommended converted from display back to system
    /// units (Slider - e.g. a Minutes slider over a Seconds powercfg value). AC/DC fall back to each other as the def branch 1.
    /// The def's dead branch 2 (every powercfg selection carries a RecommendedValueAC, so branch 1 always fires)
    /// has no catalog form; the equivalence test proves catalog == def over the whole Power population.</summary>
    internal static (string SubgroupGuid, string SettingGuid, int Ac, int Dc)? ComputePlanRecommendedWrite(Setting setting)
    {
        var pcfg = setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault();
        if (pcfg is null) return null;

        int? acSys = RecommendedSystemValue(setting, pcfg, PowerContext.AC);
        int? dcSys = RecommendedSystemValue(setting, pcfg, PowerContext.DC);
        if (!acSys.HasValue && !dcSys.HasValue) return null;

        int ac = acSys ?? dcSys ?? 0;
        int dc = dcSys ?? acSys ?? 0;
        return (pcfg.SubgroupGuid, pcfg.SettingGuid, ac, dc);
    }

    // The recommended SYSTEM powercfg value for one context: a Selection's Recommended-context-role state carries
    // Set["Power"] = StateValue.Of(the option's PowerCfgValue), so its WritePayload IS the system value the def
    // wrote from RecommendedValueAC/DC; a Slider's Numeric.Recommended is in DISPLAY units, inverted back to the
    // raw SYSTEM value the write needs. Null when there is no recommended role/value in that context.
    private static int? RecommendedSystemValue(Setting setting, PowerCfgTarget pcfg, PowerContext context)
    {
        // State-based (Selection - or, defensively, any state-carrying powercfg setting): the recommended value
        // is the Recommended-context-role state's Set["Power"] payload. Gating on States.Count (not Control ==
        // Selection) keeps a future powercfg setting whose two states happen to compute as Toggle from silently
        // dropping its plan write. A Numeric slider has no states and falls through to Numeric.Recommended below.
        if (setting.States.Count > 0)
        {
            foreach (var st in setting.States)
                if (st.Roles.Any(r => r.Kind == RoleKind.Recommended && r.Context == context)
                    && st.Set.TryGetValue(pcfg.Key, out var sv) && sv.WritePayload != null)
                    return Convert.ToInt32(sv.WritePayload);
            return null;
        }
        if (setting.Numeric is { } numeric)
        {
            // Numeric.Recommended is in DISPLAY units (the converter pre-applied system->display); the plan
            // write needs the raw SYSTEM value, so invert it - e.g. power-harddisk-timeout is a Minutes slider
            // over a Seconds powercfg value, so display 10 -> system 600.
            var display = ContextValueFor(numeric.Recommended, context);
            return display.HasValue ? ConvertDisplayToSystemUnits(display.Value, numeric.Units) : (int?)null;
        }
        return null;
    }
}
