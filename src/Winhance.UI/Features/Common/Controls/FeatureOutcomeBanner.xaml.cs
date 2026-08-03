using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Helpers;
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
/// Code-behind for <see cref="FeatureOutcomeBanner"/>. Renders the rows from
/// <see cref="FeatureOutcomeRowBuilder"/> as wrapping text with a link per setting name.
/// </summary>
public sealed partial class FeatureOutcomeBanner : UserControl, INotifyPropertyChanged
{
    /// <summary>Fallback if the resource dictionary is missing the key; keeps a bad merge from
    /// rendering an invisible icon.</summary>
    private const double OutcomeIconFontSizeFallback = 16;

    /// <summary>Drops the icon onto the text's cap height. Top alignment only lines up the layout
    /// boxes, and a TextBlock's box starts above its glyphs by the font's internal leading.</summary>
    private static readonly Thickness OutcomeIconMargin = new(0, 2, 0, 0);

    private ISettingsFeatureViewModel? _observedFeature;
    private ObservableCollection<SettingItemViewModel>? _observedSettings;
    private readonly List<SettingItemViewModel> _observedItems = new();
    private ILocalizationService? _observedLocalization;

    public FeatureOutcomeBanner()
    {
        InitializeComponent();
        Loaded += (_, _) => { Attach(); Refresh(); };
        Unloaded += (_, _) => Detach();
    }

    /// <summary>Raised when a link is clicked. The host page owns navigation, so it handles this.</summary>
    public event EventHandler<FeatureOutcomeNavigationEventArgs>? NavigationRequested;

    public static readonly DependencyProperty FeatureProperty = DependencyProperty.Register(
        nameof(Feature), typeof(ISettingsFeatureViewModel), typeof(FeatureOutcomeBanner),
        new PropertyMetadata(null, (d, _) => ((FeatureOutcomeBanner)d).Reattach()));

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
        new PropertyMetadata(null, (d, _) => ((FeatureOutcomeBanner)d).Reattach()));

    public ILocalizationService? Localization
    {
        get => (ILocalizationService?)GetValue(LocalizationProperty);
        set => SetValue(LocalizationProperty, value);
    }

    // --- Projected, bindable ---------------------------------------------------------------------

    public Visibility BannerVisibility { get; private set; } = Visibility.Collapsed;
    public bool IsBannerOpen { get; private set; }
    public string BannerMessage { get; private set; } = string.Empty;

    // --- Staying current -------------------------------------------------------------------------
    // The banner subscribes to everything that can change what it shows rather than relying on the
    // host to call Refresh, which went stale silently whenever a caller was missed.

    private void Reattach()
    {
        Attach();
        Refresh();
    }

    private void Attach()
    {
        Detach();

        _observedLocalization = Localization;
        if (_observedLocalization is { } loc)
            loc.LanguageChanged += OnLanguageChanged;

        _observedFeature = Feature;
        if (_observedFeature is not { } feature)
            return;

        feature.PropertyChanged += OnFeaturePropertyChanged;
        _observedSettings = feature.Settings;
        if (_observedSettings is not { } settings)
            return;

        settings.CollectionChanged += OnSettingsCollectionChanged;
        foreach (var setting in settings)
        {
            setting.PropertyChanged += OnSettingPropertyChanged;
            _observedItems.Add(setting);
        }
    }

    private void Detach()
    {
        if (_observedLocalization is { } loc)
            loc.LanguageChanged -= OnLanguageChanged;
        _observedLocalization = null;

        if (_observedFeature is { } feature)
            feature.PropertyChanged -= OnFeaturePropertyChanged;
        _observedFeature = null;

        // Unsubscribe from the collection we actually attached to - the feature may have swapped it.
        if (_observedSettings is { } settings)
            settings.CollectionChanged -= OnSettingsCollectionChanged;
        _observedSettings = null;

        foreach (var setting in _observedItems)
            setting.PropertyChanged -= OnSettingPropertyChanged;
        _observedItems.Clear();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    private void OnFeaturePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ISettingsFeatureViewModel.Settings))
            Reattach();
    }

    private void OnSettingsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Reattach();

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.Name))
        {
            Refresh();
        }
    }

    /// <summary>Rebuilds the rows from the feature's current settings.</summary>
    private void Refresh()
    {
        FragmentHost.Children.Clear();

        var rows = FeatureOutcomeRowBuilder.Build(Feature);
        if (rows.Count == 0)
        {
            BannerVisibility = Visibility.Collapsed;
            IsBannerOpen = false;
            Notify(nameof(BannerVisibility), nameof(IsBannerOpen));
            return;
        }

        foreach (var row in rows)
            FragmentHost.Children.Add(BuildRow(row));

        BannerMessage = Localize(
            "Overview_OutcomeBanner_Intro",
            "Winhance couldn't determine the state of some settings in this section.");
        BannerVisibility = Visibility.Visible;
        IsBannerOpen = true;
        Notify(nameof(BannerVisibility), nameof(IsBannerOpen), nameof(BannerMessage));
    }

    /// <summary>One row: the outcome's icon, then "Label: name, name, +N more" with every name a link.
    /// The names share one wrapping TextBlock so a long localized name reflows instead of overflowing;
    /// a horizontal panel of link buttons would not.</summary>
    private StackPanel BuildRow(FeatureOutcomeRow row)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        panel.Children.Add(new FluentIcon
        {
            Icon = row.Icon,
            IconVariant = FluentIcons.Common.IconVariant.Color,
            FontSize = OutcomeIconFontSize,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = OutcomeIconMargin,
        });

        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        // Localizable separator: French puts a space before the colon, CJK uses a full-width one.
        text.Inlines.Add(new Run
        {
            Text = Format(Localize("Overview_OutcomeBanner_Label", "{0}: "), row.Label),
        });

        for (int i = 0; i < row.Names.Count; i++)
        {
            if (i > 0)
                text.Inlines.Add(new Run { Text = ", " });
            text.Inlines.Add(Link(row.Names[i], row.Names[i]));
        }

        if (row.Remaining > 0)
        {
            text.Inlines.Add(new Run { Text = ", " });
            // Null target: open the feature itself rather than pre-filtering to one setting.
            text.Inlines.Add(Link(
                Format(Localize("Overview_OutcomeBanner_More", "+{0} more"), row.Remaining.ToString()),
                null));
        }

        panel.Children.Add(text);
        return panel;
    }

    /// <summary>Shared with the setting cards via the IconSizes dictionary, so one outcome is one size.</summary>
    private static double OutcomeIconFontSize =>
        Application.Current?.Resources.TryGetValue("OutcomeIconFontSize", out var value) == true
        && value is double size
            ? size
            : OutcomeIconFontSizeFallback;

    /// <summary>A name that navigates. NavigateToSection pre-applies the text as a search filter, so
    /// clicking lands on that setting already filtered.</summary>
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

    /// <summary>Substitutes {0} without string.Format, so a translator's stray brace cannot throw.</summary>
    private static string Format(string pattern, string a) => pattern.Replace("{0}", a);

    private string Localize(string key, string fallback) =>
        Localization is { } loc && loc.TryGetString(key, out var text) && !string.IsNullOrEmpty(text)
            ? text
            : fallback;

    // --- INotifyPropertyChanged -------------------------------------------------------------------
    // x:Bind OneWay needs a notification source; without one the compiler emits WMC1506.

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(params string[] names)
    {
        var handler = PropertyChanged;
        if (handler is null)
            return;
        foreach (var name in names)
            handler(this, new PropertyChangedEventArgs(name));
    }
}
