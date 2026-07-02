using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Dialogs;

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

    // Sponsor cards layout. The grid is locked to exactly CardColumns columns
    // that fill the usable content width, so cards never leave dead space on the
    // right (Marco's round-2 note). The chip packer assumes ~760px of usable
    // width (ContentDialogMaxWidth 860 minus dialog chrome/padding); the card
    // width budget is set 12px BELOW that on purpose — UniformWrapPanel derives
    // its column count from the real measured width, and sizing cards to exactly
    // 760 would make 4 columns a knife-edge (759px real width → 3 columns + a
    // ragged ghost row). The slack guarantees 4 columns with a few px of gutter.
    // ItemWidth is derived: (budget − (cols−1)×spacing) / cols
    //   = (748 − 3×12) / 4 = 178.
    private const int CardColumns = 4;
    private const double UsableContentWidth = 748d;
    private const double CardColumnSpacing = 12d;
    private const double CardRowSpacing = 14d;
    private const double CardWidth =
        (UsableContentWidth - (CardColumns - 1) * CardColumnSpacing) / CardColumns;
    private const double CardHeight = 225d;

    // Supporter chips are content-sized (measure-and-pack), not uniform cells.
    // ChipRowAvailableWidth is the usable inner width for packing chips into
    // rows: ContentDialogMaxWidth (860) minus dialog chrome/padding (~90) gives
    // ~770px of content width to flow chips across before wrapping to a new row.
    // We measure chips off the visual tree (before theme/text-scaling applies),
    // so this is deliberately a touch conservative — the supporters ScrollViewer
    // disables horizontal scrolling, so a slightly-early wrap is preferable to a
    // chip clipping off the right edge under "Make text bigger".
    private const double ChipRowAvailableWidth = 760d;
    private const double ChipColumnSpacing = 8d;
    private const double ChipRowSpacing = 8d;

    // Scrollable-region height caps for the fixed-structure modal.
    private const double SponsorScrollMaxHeight = 300d;
    private const double SupportersScrollMaxHeight = 120d;

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

        // Fixed-structure modal: the dialog itself does not scroll. Instead the
        // two unbounded regions (sponsor cards, supporter chips) each live in
        // their own height-capped ScrollViewer, so the header, section labels,
        // CTA, and disclaimer stay pinned while only those regions scroll.
        var root = new Grid
        {
            Padding = new Thickness(8),
            RowSpacing = 16
        };
        // 0: header, 1: sponsor cards (scroll), 2: supporters header + how-to,
        // 3: supporter chips (scroll), 4: CTA, 5: disclaimer, 6: checkbox (exit).
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Build methods return UIElement; Grid.SetRow needs a FrameworkElement,
        // so type these locals as FrameworkElement (every concrete element
        // returned -- Grid, StackPanel, ScrollViewer, TextBlock -- is one).
        FrameworkElement header = (FrameworkElement)BuildHeader(isDark);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var sponsorScroll = new ScrollViewer
        {
            Content = BuildSponsorCards(data),
            MaxHeight = SponsorScrollMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(sponsorScroll, 1);
        root.Children.Add(sponsorScroll);

        FrameworkElement supportersHeader = (FrameworkElement)BuildSupportersHeader(data);
        Grid.SetRow(supportersHeader, 2);
        root.Children.Add(supportersHeader);

        var supportersScroll = new ScrollViewer
        {
            Content = BuildSupportersChips(data),
            MaxHeight = SupportersScrollMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(supportersScroll, 3);
        root.Children.Add(supportersScroll);

        FrameworkElement cta = (FrameworkElement)BuildCtaRow();
        Grid.SetRow(cta, 4);
        root.Children.Add(cta);

        FrameworkElement disclaimer = (FrameworkElement)BuildDisclaimer();
        Grid.SetRow(disclaimer, 5);
        root.Children.Add(disclaimer);

        // Exit mode appends a don't-show-again checkbox into row 6.
        ConfigureButtonsAndMode(root);

        _dialog.Content = root;

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

        int cardCount = 0;
        foreach (var sponsor in QualifyingSponsors(data))
        {
            wrap.Children.Add(BuildSponsorCard(sponsor));
            cardCount++;
        }

        // Ghost "Your company here" slots keep the grid a clean rectangle and
        // always show at least one full row of invitations (Marco's round-2 note:
        // real badges, then a row of empty slots below). Fill the current partial
        // row; if the real cards already fill complete rows, add a whole ghost row.
        int remainder = cardCount % CardColumns;
        int ghostCount = remainder == 0 ? CardColumns : CardColumns - remainder;
        for (int i = 0; i < ghostCount; i++)
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
            Spacing = 4,
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
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(borderColor),
            Background = GetBrush("CardBackgroundFillColorDefaultBrush")
        };
    }

    private Border BuildExampleBadge(Windows.UI.Color tierColor)
    {
        // Content-sized pill: explicit Center so the Border hugs its text rather
        // than inheriting the parent StackPanel's default horizontal stretch
        // (which rendered the pill stretched edge-to-edge across the card).
        // Pill geometry matches the Software Apps card badges (CornerRadius 10,
        // Padding 8,2). CornerRadius 999 was dropped: at the rendered pill height
        // it produced the subpixel/clipping artefacts Marco saw (the same reason
        // SoftwareAppsPage.xaml's CardPillCornerRadius is 10, not 999). Those
        // StaticResource definitions live in SoftwareAppsPage.xaml's page-scoped
        // resources and are not reachable from code here, so the literal values
        // are used directly.
        return new Border
        {
            Background = new SolidColorBrush(tierColor),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = _localization.GetString("Sponsors_ExampleBadge").ToUpperInvariant(),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(BadgeForeground)
            }
        };
    }

    private FrameworkElement BuildLogo(SponsorEntry sponsor, Windows.UI.Color tierColor)
    {
        var image = new Image
        {
            Width = 48,
            Height = 48,
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
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(10),
            Background = GetBrush("SubtleFillColorSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = initial,
                FontSize = 20,
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

    /// <summary>
    /// Row 2 of the modal: the "Recent Supporters" section title plus a small
    /// secondary how-to-join line directly beneath it.
    /// </summary>
    private UIElement BuildSupportersHeader(SponsorsDocument? data)
    {
        var section = new StackPanel { Spacing = 2 };

        section.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Sponsors_SectionSupporters"),
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = GetBrush("TextFillColorPrimaryBrush")
        });

        section.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Sponsors_HowToJoin"),
            FontSize = 12,
            Foreground = GetBrush("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        });

        // Count line: how many supporters are shown (capped at MaxSupporters).
        // Replaces the old count-free "…and many more" overflow line. Shown only
        // when there is at least one supporter to display.
        int supporterCount = data?.Supporters?.Count ?? 0;
        if (supporterCount > 0)
        {
            int shownCount = Math.Min(MaxSupporters, supporterCount);
            section.Children.Add(new TextBlock
            {
                Text = _localization.GetString("Sponsors_RecentCount", shownCount),
                FontSize = 12,
                Foreground = GetBrush("TextFillColorTertiaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        return section;
    }

    /// <summary>
    /// Row 3 of the modal: content-sized supporter chips packed into wrapping
    /// rows. Chips are measured individually and packed greedily so each chip
    /// hugs its name (no dead space after short names), unlike the previous fixed
    /// 160px uniform cells. The supporter count line lives in the section header.
    /// </summary>
    private UIElement BuildSupportersChips(SponsorsDocument? data)
    {
        var section = new StackPanel { Spacing = 8 };

        var supporters = data?.Supporters ?? new List<SupporterEntry>();

        // Supporters array is newest-first; take the first MaxSupporters.
        var shown = supporters.Take(MaxSupporters).ToList();

        var chipBorders = new List<Border>();
        if (shown.Count == 0)
            chipBorders.Add(BuildSupporterChip(_localization.GetString("Sponsors_GhostName"), faint: true));
        else
            foreach (var supporter in shown)
                chipBorders.Add(BuildSupporterChip(supporter.Name));

        section.Children.Add(PackChips(chipBorders));

        // The overflow/count line moved up to the supporters header
        // (Sponsors_RecentCount), so the chip region renders just the chips.
        return section;
    }

    /// <summary>
    /// Measure-and-pack: measure each chip's desired width, then greedily place
    /// chips left-to-right into horizontal rows, breaking to a new row when the
    /// next chip would exceed <see cref="ChipRowAvailableWidth"/>. Returns a
    /// vertical StackPanel of horizontal row StackPanels.
    /// </summary>
    private StackPanel PackChips(List<Border> chips)
    {
        var rows = new StackPanel
        {
            Spacing = ChipRowSpacing,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        StackPanel? currentRow = null;
        double currentRowWidth = 0;

        foreach (var chip in chips)
        {
            chip.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            double chipWidth = chip.DesiredSize.Width;

            // Width this chip would add to the row, including the inter-chip gap
            // when the row already has at least one chip.
            double added = currentRow == null ? chipWidth : ChipColumnSpacing + chipWidth;

            if (currentRow == null || currentRowWidth + added > ChipRowAvailableWidth)
            {
                currentRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = ChipColumnSpacing
                };
                rows.Children.Add(currentRow);
                currentRowWidth = chipWidth;
            }
            else
            {
                currentRowWidth += added;
            }

            currentRow.Children.Add(chip);
        }

        return rows;
    }

    private Border BuildSupporterChip(string name, bool faint = false)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            Background = GetBrush("CardBackgroundFillColorDefaultBrush"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = GetBrush(faint ? "TextFillColorTertiaryBrush" : "TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.NoWrap
            }
        };
    }

    // -----------------------------------------------------------------------
    // CTA row + disclaimer
    // -----------------------------------------------------------------------

    private UIElement BuildCtaRow()
    {
        // Single CTA: the "For business" button was removed in the design
        // revision (it sent users to a separate page that confused the primary
        // ask). Only "Support Winhance" remains.
        var supportButton = new Button
        {
            Content = _localization.GetString("Sponsors_SupportButton"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };
        supportButton.Click += (_, _) => OnSupportClicked(SupportUrl);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        row.Children.Add(supportButton);
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

    private void ConfigureButtonsAndMode(Grid root)
    {
        if (_mode == SponsorsDialogMode.Normal)
        {
            _dialog.CloseButtonText = _localization.GetString("Sponsors_Close");
            return;
        }

        // Exit mode: don't-show-again checkbox in the dedicated bottom row (6),
        // and a countdown-gated close button.
        _dontShowAgainCheckbox = new CheckBox
        {
            Content = _localization.GetString("Sponsors_DontShowAgain")
        };
        Grid.SetRow(_dontShowAgainCheckbox, 6);
        root.Children.Add(_dontShowAgainCheckbox);

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
