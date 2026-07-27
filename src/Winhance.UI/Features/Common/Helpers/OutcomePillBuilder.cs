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
/// Each pill matches the colour of the icon inside it, which is the same icon the setting's own control
/// carries, so the overview and the setting can never disagree about what a colour means. The capsule
/// follows the established badge recipe (colour at 13% alpha behind, 25% border, 100% text) so these read
/// as part of the same family as Recommended and Default rather than as a foreign element.
/// </summary>
public static class OutcomePillBuilder
{
    /// <summary>Marks the pills this builder owns, so a refresh replaces only its own and never disturbs the
    /// Recommended / Default pills declared in XAML beside them.</summary>
    private const string OwnedTag = "OutcomePill";

    /// <summary>Icon size. Deliberately larger than the 12px Recommended/Default glyphs: those are flat
    /// monochrome shapes, these are multi-colour Fluent icons whose detail is unreadable that small.</summary>
    private const double IconSize = 15d;

    private readonly record struct PillSpec(
        SettingDetectionOutcome Outcome,
        string LabelKey,
        string LabelFallback,
        string StyleKey,
        string TextStyleKey,
        FluentIcons.Common.Icon Icon);

    /// <summary>Most severe first, so a feature leads with its worst problem.</summary>
    private static readonly PillSpec[] Specs =
    {
        new(SettingDetectionOutcome.Undetermined, "InfoBadge_Undetermined", "Couldn't read",
            "BadgeDangerStyle", "BadgeUndeterminedTextStyle", FluentIcons.Common.Icon.DismissCircle),
        new(SettingDetectionOutcome.Malformed, "InfoBadge_Malformed", "Wrong format",
            "BadgeCustomStyle", "BadgeMalformedTextStyle", FluentIcons.Common.Icon.ErrorCircle),
        new(SettingDetectionOutcome.Custom, "InfoBadge_Custom", "Custom",
            "BadgeUnrecognizedStyle", "BadgeUnrecognizedTextStyle", FluentIcons.Common.Icon.QuestionCircle),
    };

    /// <summary>
    /// Rebuilds the outcome pills inside <paramref name="container"/>, after whatever XAML-declared pills it
    /// already holds. Only non-zero outcomes get a pill, so a healthy feature shows nothing extra.
    /// </summary>
    public static void Rebuild(Panel container, FeatureBadgeSummary summary, ILocalizationService? localization)
    {
        // Remove this builder's previous pills (identified by Tag) and leave everything else alone.
        for (int i = container.Children.Count - 1; i >= 0; i--)
        {
            if (container.Children[i] is FrameworkElement fe && (fe.Tag as string) == OwnedTag)
                container.Children.RemoveAt(i);
        }

        foreach (var spec in Specs)
        {
            int count = CountFor(summary, spec.Outcome);
            if (count <= 0)
                continue;

            // Same denominator as the Recommended / Default pills beside it, so the whole row reads as one
            // consistent set of fractions rather than mixing populations.
            string label = Localize(localization, spec.LabelKey, spec.LabelFallback);
            container.Children.Add(BuildPill(spec, $"{label} {count}/{summary.TotalWithBadgeData}"));
        }
    }

    private static int CountFor(FeatureBadgeSummary summary, SettingDetectionOutcome outcome) => outcome switch
    {
        SettingDetectionOutcome.Undetermined => summary.UndeterminedCount,
        SettingDetectionOutcome.Malformed => summary.MalformedCount,
        _ => summary.UnrecognizedCount,
    };

    private static string Localize(ILocalizationService? localization, string key, string fallback)
    {
        var text = localization?.GetString(key);
        if (string.IsNullOrEmpty(text))
            return fallback;
        // The service returns "[Key_Not_Found]" for a missing key; don't render that to a user.
        return (text.Length >= 2 && text[0] == '[' && text[^1] == ']') ? fallback : text;
    }

    private static Border BuildPill(PillSpec spec, string text)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        content.Children.Add(new FluentIcon
        {
            Icon = spec.Icon,
            IconVariant = FluentIcons.Common.IconVariant.Color,
            FontSize = IconSize,
            VerticalAlignment = VerticalAlignment.Center,
        });

        // The text colour comes from a STYLE, never a brush fetched here: the badge brushes live in
        // ThemeDictionaries, which Application.Current.Resources.TryGetValue cannot resolve (the trap
        // documented on BoolToDimOpacityConverter). The style's {ThemeResource} setter resolves against
        // the element instead, so the colour is right in both themes AND follows a live theme switch.
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        if (TryResource<Style>(spec.TextStyleKey, out var textStyle))
            label.Style = textStyle;
        content.Children.Add(label);

        var pill = new Border { Child = content, Tag = OwnedTag };
        if (TryResource<Style>(spec.StyleKey, out var pillStyle))
            pill.Style = pillStyle;

        return pill;
    }

    /// <summary>App-level STYLE lookup. Styles live at BadgeStyles.xaml's root, which App.xaml merges, so
    /// they resolve from Application.Current.Resources. Do NOT extend this to fetch brushes: the badge
    /// brushes are ThemeDictionaries entries, which this call cannot see.</summary>
    private static bool TryResource<T>(string key, out T value) where T : class
    {
        if (Application.Current.Resources.TryGetValue(key, out var found) && found is T typed)
        {
            value = typed;
            return true;
        }
        value = null!;
        return false;
    }
}
