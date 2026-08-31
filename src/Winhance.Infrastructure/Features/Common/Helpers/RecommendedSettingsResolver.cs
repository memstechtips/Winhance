using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Helpers;

internal static class RecommendedSettingsResolver
{
    internal static string GetPowerCfgDisplayUnits(Setting setting)
    {
        if (setting.Numeric?.Units is { } units) return units;
        return setting.Targets.OfType<PowerCfgTarget>().FirstOrDefault()?.Units ?? string.Empty;
    }

    // Each State is a ComboBox option in order, its Set["Power"] holding that option's PowerCfgValue. Mirrors
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

    // Returns the index of the first State carrying an UNCONDITIONAL Recommended role. Scoped to Selection -
    // returns null for Toggle/Slider/Action/PowerPlan. A powercfg Selection's roles are CONTEXT-scoped
    // (AC/DC), so the unconditional HasRole(Recommended) does not match them. A merged Selection now exists
    // (theme-mode-windows merges the per-OS theme defaults into one build-gated Selection), but its build-scoped
    // role is the WindowsDefault, not the Recommended, so this Recommended lookup stays correct build-unaware; the
    // OS-divergent WindowsDefault is resolved via the build-aware GetDefaultIndex below.
    internal static int? GetRecommendedIndex(Setting setting)
    {
        if (setting.Control != ControlKind.Selection) return null;
        for (int i = 0; i < setting.States.Count; i++)
            if (setting.States[i].HasRole(RoleKind.Recommended)) return i;
        return null;
    }

    // Build-aware default lookup: matches a state's unconditional WindowsDefault role OR one whose build scope
    // admits `build`. A merged Selection (theme-mode-windows) declares Light as the Windows 11 WindowsDefault via a
    // build-scoped role that an unconditional-only lookup would miss; this resolves the state that is default on
    // the live build (Light on Windows 11; none on Windows 10).
    internal static int? GetDefaultIndex(Setting setting, WinBuild build)
    {
        if (setting.Control != ControlKind.Selection) return null;
        for (int i = 0; i < setting.States.Count; i++)
            if (setting.States[i].HasRole(RoleKind.WindowsDefault, build)) return i;
        return null;
    }

    // Inverse of UnitConversionHelper.ConvertToSystemUnits.
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

    // Inverse of ConvertSystemToDisplayUnits: back to raw powercfg system units.
    internal static int ConvertDisplayToSystemUnits(int displayValue, string? units) => units?.ToLowerInvariant() switch
    {
        "minutes" => displayValue * 60,
        "hours" => displayValue * 3600,
        _ => displayValue,
    };

    // Deliberately no OS-compatibility helper here: the catalog registry gates OS membership via
    // CatalogMembershipFilter.IsAvailable, and a by-id consumer that still needs the check reads
    // Setting.Availability.Allows(build) directly. Deliberately no recommended/default VALUE helper either:
    // Actions are excluded from Apply-Recommended and bulk reset, which leaves NumericRange only (all
    // powercfg), where such a helper would always return null.

    // True when a setting has a recommended value, unioning three signals: a recommended toggle state (the
    // build-aware CatalogToggleState.GetRecommended), a powercfg slider's recommended (Numeric.Recommended),
    // or a selection's recommended (registry unconditional OR powercfg context-scoped) carried as a
    // Recommended-kind role on some state. The role check is Selection-scoped so a merged toggle's
    // build-scoped role can never be caught build-unaware here (selections are never merged). A powercfg
    // recommended role is present only when its value matched a selectable option - which holds for the real
    // population, where every recommended powercfg value is a selectable option.
    internal static bool HasRecommendedValue(Setting setting, WinBuild build)
    {
        if (CatalogToggleState.GetRecommended(setting, build) is not null) return true;
        if (setting.Numeric is { } numeric && numeric.Recommended.Count > 0) return true;
        if (setting.Control == ControlKind.Selection
            && setting.States.Any(s => s.Roles.Any(r => r.Kind == RoleKind.Recommended)))
            return true;
        return false;
    }

    // As HasRecommendedValue but the WindowsDefault role / Numeric.WindowsDefault. The toggle part uses the
    // build-aware CatalogToggleState.GetDefault, so the merged (-win10) toggles - whose Windows default is
    // OS-divergent and build-scoped - agree on either OS with zero divergence.
    internal static bool HasDefaultValue(Setting setting, WinBuild build)
    {
        if (CatalogToggleState.GetDefault(setting, build) is not null) return true;
        if (setting.Numeric is { } numeric && numeric.WindowsDefault.Count > 0) return true;
        if (setting.Control == ControlKind.Selection
            && setting.States.Any(s => s.HasRole(RoleKind.WindowsDefault, build)))
            return true;
        return false;
    }

    // Builds the powercfg apply value. For a powercfg Selection, the recommended/default option is the state
    // carrying a context-scoped role (Recommended / WindowsDefault, AC / DC); that index equals
    // FindOptionIndexForPowerCfgValue for the per-mode value. For a NumericRange, the Numeric.Recommended /
    // Numeric.WindowsDefault ContextValues are already in DISPLAY units, so they are handed over directly.
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

    // The recommended AC/DC SYSTEM values PowerPlanActivationService writes to a freshly-created plan via
    // PowerWriteAC/DCValueIndex: the Recommended-role state's Set["Power"] payload (Selection) or
    // Numeric.Recommended converted display->system (Slider, e.g. Minutes over a Seconds powercfg value). AC/DC
    // fall back to each other.
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

    // The recommended SYSTEM powercfg value for one context: a Selection's Recommended-context-role state
    // carries Set["Power"] = StateValue.Of(the option's PowerCfgValue), so its WritePayload IS the system
    // value; a Slider's Numeric.Recommended is in DISPLAY units, inverted back to the raw SYSTEM value the
    // write needs. Null when there is no recommended role/value in that context.
    private static int? RecommendedSystemValue(Setting setting, PowerCfgTarget pcfg, PowerContext context)
    {
        // State-based (Selection - or, defensively, any state-carrying powercfg setting): the recommended value
        // is the Recommended-context-role state's Set["Power"] payload. Gating on States.Count (not Control ==
        // Selection) keeps a future powercfg setting whose two states happen to compute as Toggle from silently
        // dropping its plan write. A Numeric slider has no states and falls through to Numeric.Recommended below.
        // NO powercfg setting derives as Toggle today, and CatalogPowerCfgControlKindConformanceTests is what
        // holds that line - it fails the build the moment one does.
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
            // Numeric.Recommended is in DISPLAY units; the plan write needs the raw SYSTEM value, so invert
            // it - e.g. power-harddisk-timeout is a Minutes slider over a Seconds powercfg value, so display
            // 10 -> system 600.
            var display = ContextValueFor(numeric.Recommended, context);
            return display.HasValue ? ConvertDisplayToSystemUnits(display.Value, numeric.Units) : (int?)null;
        }
        return null;
    }
}
