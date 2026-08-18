using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Helpers;

/// <summary>One row of a feature's outcome banner: the settings sharing an outcome, and how many of
/// them were left unnamed.</summary>
public sealed record FeatureOutcomeRow(
    SettingDetectionOutcome Outcome,
    FluentIcons.Common.Icon Icon,
    string Label,
    IReadOnlyList<string> Names,
    int Remaining);

/// <summary>
/// Turns a feature's settings into the rows its outcome banner shows. Separate from the control so the
/// ordering and truncation rules are testable without a XAML application.
/// </summary>
public static class FeatureOutcomeRowBuilder
{
    /// <summary>Names listed per row before the rest collapse into "+N more". One shared registry value
    /// can leave eight settings unresolved at once, which would bury the card.</summary>
    public const int MaxNamesPerKind = 3;

    /// <summary>Worst first, so the rows read worst-to-least.</summary>
    private static readonly SettingDetectionOutcome[] Order =
    {
        SettingDetectionOutcome.Undetermined,
        SettingDetectionOutcome.Malformed,
        SettingDetectionOutcome.Custom,
    };

    public static IReadOnlyList<FeatureOutcomeRow> Build(ISettingsFeatureViewModel? feature)
    {
        var settings = feature?.Settings;
        if (settings is null || settings.Count == 0)
            return Array.Empty<FeatureOutcomeRow>();

        var rows = new List<FeatureOutcomeRow>();
        foreach (var outcome in Order)
        {
            var affected = settings.Where(s => s.Outcome == outcome).ToList();
            if (affected.Count == 0)
                continue;

            int shown = Math.Min(MaxNamesPerKind, affected.Count);
            rows.Add(new FeatureOutcomeRow(
                outcome,
                // Icon and label come from the setting itself, so the banner and the setting's own
                // control cannot disagree about what an outcome looks like or is called.
                affected[0].OverlayIconFor(outcome),
                affected[0].OverlayStateTextFor(outcome),
                affected.Take(shown).Select(s => s.Name).ToList(),
                affected.Count - shown));
        }

        return rows;
    }
}
