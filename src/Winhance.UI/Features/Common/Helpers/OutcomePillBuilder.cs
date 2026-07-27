using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Helpers;

/// <summary>
/// Builds the detection-outcome pills on a feature's overview card, so the overview says WHICH kind of
/// problem is inside a feature instead of a single undifferentiated "Custom N".
///
/// Generated rather than declared in XAML: the overview has six cards per page across two pages, so three
/// hand-wired pills each would be 36 near-identical blocks plus 36 wiring lines - the kind of duplication
/// that rots the moment a fourth outcome appears. One builder keeps every card identical by construction.
///
/// Each pill carries the SAME icon the setting's own control shows, from
/// <see cref="SettingItemViewModel.OverlayIconFor"/>'s vocabulary, so the overview and the setting can never
/// disagree about what a colour means.
/// </summary>
public static class OutcomePillBuilder
{
    /// <summary>Marks the pills this builder owns, so a refresh replaces only its own and never disturbs the
    /// Recommended / Default pills declared in XAML beside them.</summary>
    private const string OwnedTag = "OutcomePill";

    /// <summary>
    /// Rebuilds the outcome pills inside <paramref name="container"/>, after whatever XAML-declared pills it
    /// already holds. Only non-zero outcomes get a pill, ordered most severe first, so a healthy feature
    /// shows nothing extra and a broken one leads with its worst problem.
    /// </summary>
    public static void Rebuild(
        Panel container,
        FeatureBadgeSummary summary,
        ILocalizationService? localization)
    {
        // Remove this builder's previous pills (identified by Tag) and leave everything else alone.
        for (int i = container.Children.Count - 1; i >= 0; i--)
        {
            if (container.Children[i] is FrameworkElement fe && (fe.Tag as string) == OwnedTag)
                container.Children.RemoveAt(i);
        }

        // Severity order: red (unreadable) -> yellow (wrong format) -> blue (unrecognized value).
        foreach (var (count, outcome, key, fallback) in new[]
        {
            (summary.UndeterminedCount, SettingDetectionOutcome.Undetermined, "InfoBadge_Undetermined", "Couldn't read"),
            (summary.MalformedCount, SettingDetectionOutcome.Malformed, "InfoBadge_Malformed", "Wrong format"),
            (summary.UnrecognizedCount, SettingDetectionOutcome.Custom, "InfoBadge_Custom", "Custom"),
        })
        {
            if (count <= 0)
                continue;
            container.Children.Add(BuildPill(count, outcome, Localize(localization, key, fallback)));
        }
    }

    private static string Localize(ILocalizationService? localization, string key, string fallback)
    {
        var text = localization?.GetString(key);
        if (string.IsNullOrEmpty(text))
            return fallback;
        // The service returns "[Key_Not_Found]" for a missing key; don't render that to a user.
        return (text.Length >= 2 && text[0] == '[' && text[^1] == ']') ? fallback : text;
    }

    private static Border BuildPill(int count, SettingDetectionOutcome outcome, string label)
    {
        var icon = new FluentIcon
        {
            Icon = IconFor(outcome),
            IconVariant = FluentIcons.Common.IconVariant.Color,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var text = new TextBlock
        {
            Text = $"{label} {count}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (Application.Current.Resources.TryGetValue("BadgeTextStyle", out var textStyle) && textStyle is Style ts)
            text.Style = ts;

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        content.Children.Add(icon);
        content.Children.Add(text);

        var pill = new Border { Child = content, Tag = OwnedTag };
        // Reuse the existing capsule so these sit in the same visual vocabulary as Recommended / Default.
        // The colour lives in the icon, not the capsule, so one neutral style serves all three outcomes.
        if (Application.Current.Resources.TryGetValue("BadgeCustomStyle", out var pillStyle) && pillStyle is Style ps)
            pill.Style = ps;

        return pill;
    }

    /// <summary>The same outcome-to-icon vocabulary the settings cards use.</summary>
    private static FluentIcons.Common.Icon IconFor(SettingDetectionOutcome outcome) => outcome switch
    {
        SettingDetectionOutcome.Malformed => FluentIcons.Common.Icon.ErrorCircle,
        SettingDetectionOutcome.Undetermined => FluentIcons.Common.Icon.DismissCircle,
        _ => FluentIcons.Common.Icon.QuestionCircle,
    };
}
