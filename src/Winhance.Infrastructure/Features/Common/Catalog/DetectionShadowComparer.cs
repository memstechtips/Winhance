using System;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>The verdict for one setting in a detection shadow run.</summary>
public enum ShadowVerdict { Match, Diff, Unpaired, Skipped }

/// <summary>One setting's old-vs-new comparison row.</summary>
public sealed record ShadowRow(string Id, string OldState, string NewState, ShadowVerdict Verdict);

/// <summary>Compares the old discovery result and the new engine result for one setting, normalizing both to a
/// comparable label per mechanism, mirroring the equivalence harness: a toggle by its enabled/disabled state, a
/// selection by its resolved option label, a numeric by its raw value. Pure (no IO), so it is unit-testable off
/// Windows. The selection baseline is the post-ResolveComboBoxStates CurrentValue (the ComboBoxResolver index the
/// harness validated against), so the option label resolves the same way on both sides.</summary>
public static class DetectionShadowComparer
{
    public static ShadowRow Compare(
        SettingDefinition oldDefinition,
        SettingStateResult oldResult,
        Setting? newSetting,
        CatalogDetectionResult? newResult)
    {
        if (newSetting is null || newResult is null)
            return new ShadowRow(oldDefinition.Id, OldLabel(oldDefinition, oldResult), "(none)", ShadowVerdict.Unpaired);

        // Actions carry no detectable state - there is nothing to compare.
        if (oldDefinition.InputType == InputType.Action)
            return new ShadowRow(oldDefinition.Id, "-", "-", ShadowVerdict.Skipped);

        string oldLabel = OldLabel(oldDefinition, oldResult);
        string newLabel = NewLabel(oldDefinition.InputType, newResult);
        var verdict = string.Equals(oldLabel, newLabel, StringComparison.Ordinal)
            ? ShadowVerdict.Match
            : ShadowVerdict.Diff;
        return new ShadowRow(oldDefinition.Id, oldLabel, newLabel, verdict);
    }

    private static string OldLabel(SettingDefinition def, SettingStateResult r) => def.InputType switch
    {
        InputType.Selection => r.CurrentValue is int index ? LabelForIndex(def, index) : "Custom",
        InputType.NumericRange => r.CurrentValue?.ToString() ?? "absent",
        _ => r.IsEnabled ? "Enabled" : "Disabled",
    };

    private static string NewLabel(InputType inputType, CatalogDetectionResult r) => inputType switch
    {
        InputType.NumericRange => r.Value?.ToString() ?? "absent",
        _ => r.StateLabel ?? "Custom",
    };

    private static string LabelForIndex(SettingDefinition def, int index)
    {
        var options = def.ComboBox?.Options;
        if (options is null || index < 0 || index >= options.Count)
            return "Custom";
        return options[index].DisplayName;
    }
}
