using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>Asks the host page to navigate. <see cref="SettingName"/> is null for the "+N more" link,
/// which opens the feature without pre-filtering to any one setting.</summary>
public sealed class FeatureOutcomeNavigationEventArgs : EventArgs
{
    public string SectionKey { get; set; } = string.Empty;
    public string? SettingName { get; set; }
}

/// <summary>
/// Code-behind for <see cref="FeatureOutcomeBanner"/>. Names the settings Winhance could not place and
/// turns each into a link to it.
/// </summary>
public sealed partial class FeatureOutcomeBanner : UserControl, INotifyPropertyChanged
{
    /// <summary>How many names to list per kind before collapsing into "+N more". Three keeps the row to
    /// one or two wrapped lines even with long localized names; a shared registry value can leave eight
    /// settings unresolved at once (the UserPreferencesMask case), and listing all of them would turn a
    /// summary card into a wall of text.</summary>
    private const int MaxNamesPerKind = 3;

    public FeatureOutcomeBanner() => InitializeComponent();

    /// <summary>Raised when a link is clicked. The host page owns navigation, so it handles this.</summary>
    public event EventHandler<FeatureOutcomeNavigationEventArgs>? NavigationRequested;

    public static readonly DependencyProperty FeatureProperty = DependencyProperty.Register(
        nameof(Feature), typeof(ISettingsFeatureViewModel), typeof(FeatureOutcomeBanner),
        new PropertyMetadata(null, (d, _) => ((FeatureOutcomeBanner)d).Refresh()));

    public ISettingsFeatureViewModel? Feature
    {
        get => (ISettingsFeatureViewModel?)GetValue(FeatureProperty);
        set => SetValue(FeatureProperty, value);
    }

    /// <summary>The key the host page's NavigateToSection expects ("Gaming", "Taskbar", ...).</summary>
    public static readonly DependencyProperty SectionKeyProperty = DependencyProperty.Register(
        nameof(SectionKey), typeof(string), typeof(FeatureOutcomeBanner),
        new PropertyMetadata(string.Empty));

    public string SectionKey
    {
        get => (string)GetValue(SectionKeyProperty);
        set => SetValue(SectionKeyProperty, value);
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

    /// <summary>Most severe first, so the rows read worst-to-least. Each icon matches the one those
    /// settings carry on their own controls, so a colour means one thing everywhere.</summary>
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
        FragmentHost.Children.Clear();

        var settings = Feature?.Settings;
        if (settings is null || settings.Count == 0)
        {
            Hide();
            return;
        }

        bool any = false;
        foreach (var (outcome, labelKey, labelFallback, icon) in Kinds)
        {
            var affected = settings.Where(s => s.Outcome == outcome).ToList();
            if (affected.Count == 0)
                continue;

            any = true;
            FragmentHost.Children.Add(BuildRow(icon, Localize(labelKey, labelFallback), affected));
        }

        if (!any)
        {
            Hide();
            return;
        }

        BannerMessage = Localize(
            "Overview_OutcomeBanner_Intro",
            "Winhance couldn't determine the state of some settings in this section.");
        BannerVisibility = Visibility.Visible;
        IsBannerOpen = true;
        NotifyAll();
    }

    /// <summary>One row: the outcome's icon, then "Label: name, name, +N more" where every name is a
    /// link. The names live in a single wrapping TextBlock so a long localized name reflows instead of
    /// overflowing the card - a horizontal panel of link buttons would not.</summary>
    private StackPanel BuildRow(
        FluentIcons.Common.Icon icon, string label, IReadOnlyList<SettingItemViewModel> affected)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        row.Children.Add(new FluentIcon
        {
            Icon = icon,
            IconVariant = FluentIcons.Common.IconVariant.Color,
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
        });

        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        // Localizable separator, not a hardcoded ": " - French puts a space before the colon and
        // CJK uses a full-width one.
        text.Inlines.Add(new Run { Text = Format(Localize("Overview_OutcomeBanner_Label", "{0}: "), label) });

        int shown = Math.Min(MaxNamesPerKind, affected.Count);
        for (int i = 0; i < shown; i++)
        {
            if (i > 0)
                text.Inlines.Add(new Run { Text = ", " });
            text.Inlines.Add(Link(affected[i].Name, affected[i].Name));
        }

        int remaining = affected.Count - shown;
        if (remaining > 0)
        {
            text.Inlines.Add(new Run { Text = ", " });
            // Null target: open the feature itself rather than pre-filtering to one setting.
            text.Inlines.Add(Link(
                Format(Localize("Overview_OutcomeBanner_More", "+{0} more"), remaining.ToString()),
                null));
        }

        row.Children.Add(text);
        return row;
    }

    /// <summary>A name that navigates. The host's NavigateToSection pre-applies the text as a search
    /// filter, so clicking lands on that setting already filtered rather than somewhere to hunt from.</summary>
    private Hyperlink Link(string display, string? settingName)
    {
        var link = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
        link.Inlines.Add(new Run { Text = display });
        link.Click += (_, _) => NavigationRequested?.Invoke(this, new FeatureOutcomeNavigationEventArgs
        {
            SectionKey = SectionKey,
            SettingName = settingName,
        });
        return link;
    }

    private void Hide()
    {
        BannerVisibility = Visibility.Collapsed;
        IsBannerOpen = false;
        NotifyAll();
    }

    /// <summary>Substitutes {0} without string.Format, so a translator's stray brace cannot throw a
    /// FormatException at runtime on a machine we never see.</summary>
    private static string Format(string pattern, string a) => pattern.Replace("{0}", a);

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
