using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Dialogs;

/// <summary>
/// Display mode for the sponsors dialog.
/// <see cref="Normal"/> shows a simple Close button. <see cref="Exit"/> adds a
/// "don't show again" checkbox and a countdown-gated close button (used when the
/// dialog is shown on app exit so the supporter list is actually read).
/// </summary>
public enum SponsorsDialogMode
{
    Normal,
    Exit
}

/// <summary>
/// Encapsulates the UI-building logic for the in-app sponsors page dialog.
/// Mirrors <see cref="ConfigImportDialogBuilder"/>: the builder constructs the
/// ContentDialog programmatically and exposes <see cref="ExtractResult"/> for the
/// caller (DialogService) to read after ShowAsync returns. The caller owns
/// ConfigureDialog, the semaphore, and ShowAsync.
/// </summary>
internal class SponsorsDialogBuilder
{
    private const string SupportUrl = "https://store.memstechtips.com/winhance/";
    private const string BusinessUrl = "https://store.memstechtips.com/winhance/#business";

    // Sponsor cards layout
    private const double CardWidth = 220d;
    private const double CardHeight = 240d;
    private const double CardColumnSpacing = 14d;
    private const double CardRowSpacing = 14d;

    // Supporter chips layout. UniformWrapPanel needs a positive ItemWidth to
    // wrap (ItemWidth <= 0 collapses to a single non-wrapping row); a uniform
    // cell width is the house-consistent way to get a wrapping flow here.
    private const double ChipWidth = 160d;
    private const double ChipHeight = 32d;
    private const double ChipColumnSpacing = 8d;
    private const double ChipRowSpacing = 8d;

    private const int MaxSupporters = 48;
    private const int CountdownSeconds = 3;

    // Tier accent colors (used for borders, chips, monograms).
    private static readonly Windows.UI.Color EmeraldColor = Windows.UI.Color.FromArgb(0xFF, 0x50, 0xC8, 0x78);
    private static readonly Windows.UI.Color GoldColor = Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xD7, 0x00);
    private static readonly Windows.UI.Color BadgeForeground = Windows.UI.Color.FromArgb(0xFF, 0x16, 0x13, 0x00);

    private readonly ILocalizationService _localization;
    private readonly ISponsorsService _sponsorsService;

    // Result state
    private CheckBox? _dontShowAgainCheckbox;
    private bool _supportClicked;

    // Exit-mode countdown state
    private SponsorsDialogMode _mode;
    private bool _countdownDone;
    private int _countdownRemaining = CountdownSeconds;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _countdownTimer;
    private ContentDialog _dialog = null!;

    public SponsorsDialogBuilder(ILocalizationService localization, ISponsorsService sponsorsService)
    {
        _localization = localization;
        _sponsorsService = sponsorsService;
    }

    /// <summary>
    /// Builds the ContentDialog. The caller is responsible for calling
    /// ConfigureDialog and ShowAsync, then <see cref="ExtractResult"/>.
    /// </summary>
    public ContentDialog Build(SponsorsDocument? data, SponsorsDialogMode mode, XamlRoot xamlRoot)
    {
        _mode = mode;
        bool isDark = (xamlRoot.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;

        _dialog = new ContentDialog
        {
            DefaultButton = ContentDialogButton.Close
        };

        // ContentDialog default MaxWidth is 548px -- override so the wide
        // card grid has room to lay out.
        _dialog.Resources["ContentDialogMaxWidth"] = 860d;
        _dialog.Resources["ContentDialogMaxHeight"] = 720d;

        var rootPanel = new StackPanel
        {
            Spacing = 20,
            Padding = new Thickness(8)
        };

        rootPanel.Children.Add(BuildHeader(isDark));
        rootPanel.Children.Add(BuildSponsorCards(data));
        rootPanel.Children.Add(BuildSupportersSection(data));
        rootPanel.Children.Add(BuildCtaRow());
        rootPanel.Children.Add(BuildDisclaimer());

        ConfigureButtonsAndMode(rootPanel);

        _dialog.Content = new ScrollViewer
        {
            Content = rootPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        return _dialog;
    }

    /// <summary>
    /// Extracts the result after the dialog has been shown.
    /// SupportClicked is true if the user clicked a support/business button.
    /// DontShowAgain is the exit-mode checkbox state (always false in Normal mode).
    /// </summary>
    public (bool SupportClicked, bool DontShowAgain) ExtractResult()
    {
        bool dontShowAgain = _mode == SponsorsDialogMode.Exit && _dontShowAgainCheckbox?.IsChecked == true;
        return (_supportClicked, dontShowAgain);
    }

    // -----------------------------------------------------------------------
    // Header
    // -----------------------------------------------------------------------

    private UIElement BuildHeader(bool isDark)
    {
        var logoUri = isDark
            ? "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"
            : "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png";

        var logo = new Image
        {
            Source = new BitmapImage(new Uri(logoUri)),
            Width = 40,
            Height = 40,
            VerticalAlignment = VerticalAlignment.Top
        };

        var title = new TextBlock
        {
            Text = _localization.GetString("Sponsors_Title"),
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = GetBrush("TextFillColorPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };

        var subtitle = new TextBlock
        {
            Text = _localization.GetString("Sponsors_Subtitle"),
            FontSize = 13,
            Foreground = GetBrush("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };

        var textPanel = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        textPanel.Children.Add(title);
        textPanel.Children.Add(subtitle);

        // Grid (not horizontal StackPanel) so the subtitle wraps in the
        // remaining width rather than running off the edge.
        var header = new Grid { ColumnSpacing = 14 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(logo, 0);
        Grid.SetColumn(textPanel, 1);
        header.Children.Add(logo);
        header.Children.Add(textPanel);
        return header;
    }

    // -----------------------------------------------------------------------
    // Sponsor cards
    // -----------------------------------------------------------------------

    private UIElement BuildSponsorCards(SponsorsDocument? data)
    {
        var wrap = new Controls.UniformWrapPanel
        {
            ItemWidth = CardWidth,
            ItemHeight = CardHeight,
            ColumnSpacing = CardColumnSpacing,
            RowSpacing = CardRowSpacing,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        foreach (var sponsor in QualifyingSponsors(data))
            wrap.Children.Add(BuildSponsorCard(sponsor));

        // Always render the ghost slot inviting a new sponsor; with no data it
        // is the only card so the section still looks intentional.
        wrap.Children.Add(BuildGhostCard());

        return wrap;
    }

    /// <summary>
    /// Sponsors with tier (case-insensitive) "gold" or "emerald" and no Until date.
    /// Emerald first, then gold; stable within tier.
    /// </summary>
    private static IEnumerable<SponsorEntry> QualifyingSponsors(SponsorsDocument? data)
    {
        if (data?.Sponsors is not { Count: > 0 } sponsors)
            return Enumerable.Empty<SponsorEntry>();

        return sponsors
            .Where(s => s.Until == null && (IsEmerald(s) || IsGold(s)))
            .OrderBy(s => IsEmerald(s) ? 0 : 1); // OrderBy is stable in .NET
    }

    private static bool IsEmerald(SponsorEntry s) =>
        string.Equals(s.Tier, "emerald", StringComparison.OrdinalIgnoreCase);

    private static bool IsGold(SponsorEntry s) =>
        string.Equals(s.Tier, "gold", StringComparison.OrdinalIgnoreCase);

    private Border BuildSponsorCard(SponsorEntry sponsor)
    {
        bool isEmerald = IsEmerald(sponsor);
        var tierColor = isEmerald ? EmeraldColor : GoldColor;

        // Border alpha: emerald ~60%, gold ~50%.
        byte borderAlpha = isEmerald ? (byte)0x99 : (byte)0x80;
        var borderColor = Windows.UI.Color.FromArgb(borderAlpha, tierColor.R, tierColor.G, tierColor.B);

        var cardContent = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Optional example badge (first in the card so it reads as a pill on top).
        if (sponsor.Example)
            cardContent.Children.Add(BuildExampleBadge(tierColor));

        // Tier chip.
        cardContent.Children.Add(new TextBlock
        {
            Text = (sponsor.Tier ?? string.Empty).ToUpperInvariant(),
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(tierColor),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        // Logo (or monogram fallback).
        cardContent.Children.Add(BuildLogo(sponsor, tierColor));

        // Name.
        cardContent.Children.Add(new TextBlock
        {
            Text = sponsor.Name,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = GetBrush("TextFillColorPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        // City (optional).
        if (!string.IsNullOrEmpty(sponsor.City))
            cardContent.Children.Add(BuildSecondaryLine(sponsor.City));

        // Contact (optional).
        if (!string.IsNullOrEmpty(sponsor.Contact))
            cardContent.Children.Add(BuildSecondaryLine(sponsor.Contact));

        // URL (https-only) link.
        var urlLink = BuildUrlLink(sponsor.Url);
        if (urlLink != null)
            cardContent.Children.Add(urlLink);

        return new Border
        {
            Child = cardContent,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(borderColor),
            Background = GetBrush("CardBackgroundFillColorDefaultBrush")
        };
    }

    private Border BuildExampleBadge(Windows.UI.Color tierColor)
    {
        return new Border
        {
            Background = new SolidColorBrush(tierColor),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = _localization.GetString("Sponsors_ExampleBadge").ToUpperInvariant(),
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(BadgeForeground)
            }
        };
    }

    private FrameworkElement BuildLogo(SponsorEntry sponsor, Windows.UI.Color tierColor)
    {
        var image = new Image
        {
            Width = 56,
            Height = 56,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var monogram = BuildMonogram(sponsor, tierColor);
        monogram.Visibility = Visibility.Collapsed;

        // Try the live logo first; on failure swap to the bundled snapshot, and
        // if that's also unavailable fall back to the monogram.
        var bitmap = new BitmapImage(new Uri(_sponsorsService.GetLogoUri(sponsor)));
        image.Source = bitmap;

        bool triedBundled = false;
        image.ImageFailed += (_, _) =>
        {
            string? bundled = _sponsorsService.GetBundledLogoPath(sponsor);
            if (!triedBundled && bundled != null)
            {
                triedBundled = true;
                image.Source = new BitmapImage(new Uri(bundled));
                return;
            }

            image.Visibility = Visibility.Collapsed;
            monogram.Visibility = Visibility.Visible;
        };

        var container = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        container.Children.Add(image);
        container.Children.Add(monogram);
        return container;
    }

    private Border BuildMonogram(SponsorEntry sponsor, Windows.UI.Color tierColor)
    {
        string initial = string.IsNullOrEmpty(sponsor.Name)
            ? "?"
            : sponsor.Name.Substring(0, 1).ToUpperInvariant();

        return new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(10),
            Background = GetBrush("SubtleFillColorSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = initial,
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(tierColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private TextBlock BuildSecondaryLine(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = GetBrush("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    /// <summary>
    /// Returns a HyperlinkButton for the sponsor URL, but only when it is a
    /// non-empty absolute https URI. Content is the host without a leading "www.".
    /// </summary>
    private HyperlinkButton? BuildUrlLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return null;

        string host = uri.Host;
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host.Substring(4);

        return new HyperlinkButton
        {
            Content = host,
            NavigateUri = uri,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(0)
        };
    }

    private Border BuildGhostCard()
    {
        return new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            BorderBrush = GetBrush("ControlStrokeColorDefaultBrush"),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = new TextBlock
            {
                Text = _localization.GetString("Sponsors_GhostCompany"),
                FontSize = 13,
                Foreground = GetBrush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    // -----------------------------------------------------------------------
    // Supporters
    // -----------------------------------------------------------------------

    private UIElement BuildSupportersSection(SponsorsDocument? data)
    {
        var section = new StackPanel { Spacing = 10 };

        section.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Sponsors_SectionSupporters"),
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = GetBrush("TextFillColorPrimaryBrush")
        });

        var supporters = data?.Supporters ?? new List<SupporterEntry>();

        var chips = new Controls.UniformWrapPanel
        {
            ItemWidth = ChipWidth,
            ItemHeight = ChipHeight,
            ColumnSpacing = ChipColumnSpacing,
            RowSpacing = ChipRowSpacing,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // Supporters array is newest-first; take the first MaxSupporters.
        var shown = supporters.Take(MaxSupporters).ToList();
        foreach (var supporter in shown)
            chips.Children.Add(BuildSupporterChip(supporter.Name));

        if (shown.Count == 0)
            chips.Children.Add(BuildSupporterChip(_localization.GetString("Sponsors_GhostName"), faint: true));

        section.Children.Add(chips);

        if (supporters.Count > MaxSupporters)
        {
            section.Children.Add(new TextBlock
            {
                Text = _localization.GetString("Sponsors_MoreSupporters", supporters.Count - MaxSupporters),
                FontSize = 12,
                Foreground = GetBrush("TextFillColorTertiaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        return section;
    }

    private Border BuildSupporterChip(string name, bool faint = false)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            Background = GetBrush("CardBackgroundFillColorDefaultBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = GetBrush(faint ? "TextFillColorTertiaryBrush" : "TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
    }

    // -----------------------------------------------------------------------
    // CTA row + disclaimer
    // -----------------------------------------------------------------------

    private UIElement BuildCtaRow()
    {
        var supportButton = new Button
        {
            Content = _localization.GetString("Sponsors_SupportButton"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };
        supportButton.Click += (_, _) => OnSupportClicked(SupportUrl);

        var businessButton = new Button
        {
            Content = _localization.GetString("Sponsors_BusinessButton")
        };
        businessButton.Click += (_, _) => OnSupportClicked(BusinessUrl);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        row.Children.Add(supportButton);
        row.Children.Add(businessButton);
        return row;
    }

    private void OnSupportClicked(string url)
    {
        _supportClicked = true;
        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(url));

        // In exit mode, clicking a support button should dismiss the dialog
        // (the countdown-gated close is bypassed for an intentional support action).
        if (_mode == SponsorsDialogMode.Exit)
            _dialog.Hide();
    }

    private UIElement BuildDisclaimer()
    {
        return new TextBlock
        {
            Text = _localization.GetString("Sponsors_Disclaimer"),
            FontSize = 11,
            Foreground = GetBrush("TextFillColorTertiaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };
    }

    // -----------------------------------------------------------------------
    // Buttons + mode wiring
    // -----------------------------------------------------------------------

    private void ConfigureButtonsAndMode(StackPanel rootPanel)
    {
        if (_mode == SponsorsDialogMode.Normal)
        {
            _dialog.CloseButtonText = _localization.GetString("Sponsors_Close");
            return;
        }

        // Exit mode: don't-show-again checkbox at the bottom of the content,
        // and a countdown-gated close button.
        _dontShowAgainCheckbox = new CheckBox
        {
            Content = _localization.GetString("Sponsors_DontShowAgain")
        };
        rootPanel.Children.Add(_dontShowAgainCheckbox);

        _dialog.CloseButtonText = _localization.GetString("Sponsors_ExitCountdown", CountdownSeconds);

        // Block the close button until the countdown elapses. A support-click
        // Hide() is allowed through because _supportClicked is set first.
        _dialog.Closing += (_, e) =>
        {
            if (!_countdownDone && !_supportClicked)
                e.Cancel = true;
        };

        _dialog.Opened += (_, _) => StartCountdown();
        _dialog.Closed += (_, _) => StopCountdown();
    }

    private void StartCountdown()
    {
        _countdownRemaining = CountdownSeconds;
        _countdownTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.IsRepeating = true;
        _countdownTimer.Tick += (_, _) =>
        {
            _countdownRemaining--;
            if (_countdownRemaining > 0)
            {
                _dialog.CloseButtonText = _localization.GetString("Sponsors_ExitCountdown", _countdownRemaining);
            }
            else
            {
                _countdownDone = true;
                _dialog.CloseButtonText = _localization.GetString("Sponsors_Exit");
                StopCountdown();
            }
        };
        _countdownTimer.Start();
    }

    private void StopCountdown()
    {
        if (_countdownTimer != null)
        {
            _countdownTimer.Stop();
            _countdownTimer = null;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves a theme-aware brush from the application resources. The dialog's
    /// RequestedTheme (set by DialogService.ConfigureDialog) drives which theme
    /// variant these ThemeResource-backed brushes render.
    /// </summary>
    private static Brush GetBrush(string key) => (Brush)Application.Current.Resources[key];
}
