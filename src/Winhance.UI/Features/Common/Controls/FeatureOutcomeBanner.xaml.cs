using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>One kind of unresolved setting found in a feature: its icon and its "Label: N" text.</summary>
public sealed class FeatureOutcomeFragment
{
    public FluentIcons.Common.Icon Icon { get; init; }
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// Code-behind for <see cref="FeatureOutcomeBanner"/>. Turns a feature's aggregated outcome counts into
/// one informational banner carrying a coloured chip per kind found.
/// </summary>
public sealed partial class FeatureOutcomeBanner : UserControl, INotifyPropertyChanged
{
    public FeatureOutcomeBanner() => InitializeComponent();

    public static readonly DependencyProperty FeatureProperty = DependencyProperty.Register(
        nameof(Feature), typeof(ISettingsFeatureViewModel), typeof(FeatureOutcomeBanner),
        new PropertyMetadata(null, (d, _) => ((FeatureOutcomeBanner)d).Refresh()));

    public ISettingsFeatureViewModel? Feature
    {
        get => (ISettingsFeatureViewModel?)GetValue(FeatureProperty);
        set => SetValue(FeatureProperty, value);
    }

    /// <summary>Supplies the localized strings. Set by the host page, which owns the service.</summary>
    public static readonly DependencyProperty LocalizationProperty = DependencyProperty.Register(
        nameof(Localization), typeof(ILocalizationService), typeof(FeatureOutcomeBanner),
        new PropertyMetadata(null, (d, _) => ((FeatureOutcomeBanner)d).Refresh()));

    public ILocalizationService? Localization
    {
        get => (ILocalizationService?)GetValue(LocalizationProperty);
        set => SetValue(LocalizationProperty, value);
    }

    // --- Projected, bindable ---------------------------------------------------------------------

    public Visibility BannerVisibility { get; private set; } = Visibility.Collapsed;
    public bool IsBannerOpen { get; private set; }
    public string BannerMessage { get; private set; } = string.Empty;

    /// <summary>One entry per outcome kind actually present, most severe first.</summary>
    public ObservableCollection<FeatureOutcomeFragment> Fragments { get; } = new();

    /// <summary>Most severe first, so the chips read worst-to-least. The icons match the ones the
    /// affected settings carry on their own controls.</summary>
    private static readonly (SettingDetectionOutcome Outcome, string LabelKey, string LabelFallback,
        FluentIcons.Common.Icon Icon)[] Kinds =
    {
        (SettingDetectionOutcome.Undetermined, "InfoBadge_Undetermined", "Couldn't read",
            FluentIcons.Common.Icon.DismissCircle),
        (SettingDetectionOutcome.Malformed, "InfoBadge_Malformed", "Wrong format",
            FluentIcons.Common.Icon.ErrorCircle),
        (SettingDetectionOutcome.Custom, "InfoBadge_Custom", "Not recognized",
            FluentIcons.Common.Icon.QuestionCircle),
    };

    /// <summary>Recomputes from the feature's current settings. Called by the host page on the same
    /// events that refresh the overview pills, so the banner never lags behind them.</summary>
    public void Refresh()
    {
        Fragments.Clear();

        if (Feature is not { } feature)
        {
            Hide();
            return;
        }

        var summary = FeatureBadgeAggregator.Aggregate(feature);
        if (summary.UnresolvedCount == 0)
        {
            Hide();
            return;
        }

        foreach (var (outcome, labelKey, labelFallback, icon) in Kinds)
        {
            int count = CountFor(summary, outcome);
            if (count <= 0)
                continue;

            // No denominator. "1 of 114" reads as a ratio, which is right for "Recommended 109/114"
            // but not here - how many settings the feature HAS tells you nothing about the one that is
            // broken. The count alone is what you need: how many to look for once you click in.
            Fragments.Add(new FeatureOutcomeFragment
            {
                Icon = icon,
                Text = Format(
                    Localize("Overview_OutcomeBanner_Fragment", "{0}: {1}"),
                    Localize(labelKey, labelFallback),
                    count.ToString()),
            });
        }

        BannerMessage = Localize(
            "Overview_OutcomeBanner_Intro",
            "Winhance couldn't determine the state of some settings in this section.");

        BannerVisibility = Visibility.Visible;
        IsBannerOpen = true;
        NotifyAll();
    }

    private void Hide()
    {
        BannerVisibility = Visibility.Collapsed;
        IsBannerOpen = false;
        NotifyAll();
    }

    private static int CountFor(FeatureBadgeSummary s, SettingDetectionOutcome outcome) => outcome switch
    {
        SettingDetectionOutcome.Undetermined => s.UndeterminedCount,
        SettingDetectionOutcome.Malformed => s.MalformedCount,
        _ => s.UnrecognizedCount,
    };

    /// <summary>Substitutes {0}/{1} without string.Format, so a translator's stray brace cannot throw a
    /// FormatException at runtime on a machine we never see.</summary>
    private static string Format(string pattern, string a, string b) =>
        pattern.Replace("{0}", a).Replace("{1}", b);

    private string Localize(string key, string fallback)
    {
        var text = Localization?.GetString(key);
        if (string.IsNullOrEmpty(text))
            return fallback;
        return (text.Length >= 2 && text[0] == '[' && text[^1] == ']') ? fallback : text;
    }

    // --- INotifyPropertyChanged -------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyAll()
    {
        var handler = PropertyChanged;
        if (handler is null)
            return;
        foreach (var name in new[]
                 {
                     nameof(BannerVisibility), nameof(IsBannerOpen), nameof(BannerMessage),
                 })
        {
            handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
