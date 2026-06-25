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
/// and stateless Actions - by pairing the def with its <see cref="SettingCatalog"/> Setting and running
/// <see cref="ApplyPlanBuilder"/>. Returns null (-> old apply) for:
///   - an unpaired def (no SettingCatalog peer, e.g. the -win10 merged variants until the 6.5 alias),
///   - resetToDefault (the apply-only [x, null] reset-write divergence - roadmap 3A - is not modelled yet, so the
///     new WindowsDefault state would write x where the old reset deletes),
///   - NumericRange sliders (the display->system value conversion is not ported here),
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
        // Reset-to-default still routes through the old apply: the apply-only reset write-payload for the
        // [x, null] ExplorerCustomizations settings (roadmap 3A) is not modelled, so applying the WindowsDefault
        // state would write x where the old reset deletes. Keep reset on the proven path.
        if (resetToDefault)
            return null;

        var setting = catalog.FirstOrDefault(s => s.Id == def.Id);
        if (setting is null)
            return null; // unpaired -> old apply

        // Custom-detector / dynamic-option settings (DNS, system-tray, system-restore, power-plan) apply via paths
        // the plan builder does not reproduce (and their special handlers run earlier in the funnel anyway).
        if (setting.Detector is not null || setting.OptionSource is not null)
            return null;

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

            default: // NumericRange and anything else -> old apply
                return null;
        }
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
