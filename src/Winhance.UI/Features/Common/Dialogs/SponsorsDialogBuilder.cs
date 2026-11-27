using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Controls;
using static Winhance.UI.Features.Common.Helpers.CodeBuiltStyle;

namespace Winhance.UI.Features.Common.Dialogs;

// The caller (DialogService) owns ConfigureDialog, the semaphore and ShowAsync.
internal class SponsorsDialogBuilder
{
    // The ref tag tells app-driven store visits apart from the website's in the store's logs.
    private const string SupportUrl = "https://store.memstechtips.com/winhance/?ref=";
    private const string BusinessSponsorUrl = SupportUrl + "app-sponsor#business";

    // One row of sponsor cards. The card budget sits below the dialog's usable width (max width minus
    // ~90px of chrome and padding) on purpose: UniformWrapPanel derives its column count from the real
    // measured width, and sizing cards to exactly that width makes the third column a knife-edge.
    private const double DialogMaxWidth = 620d;
    private const int CardColumns = 3;
    private const double CardBudgetWidth = 510d;
    private const double CardColumnSpacing = 12d;
    private const double CardRowSpacing = 12d;
    private const double CardWidth = (CardBudgetWidth - (CardColumns - 1) * CardColumnSpacing) / CardColumns;
    private const double CardHeight = 191d;
    private const double CardCornerRadius = 12d;
    private const double CardLogoSize = 56d;
    // One row, plus half of the next when there is one, so more sponsors read as scrollable rather than cut off.
    private const double SponsorWallMaxHeight = CardHeight * 1.5 + CardRowSpacing;

    // The supporters array is newest first, so the wall reads as who chipped in lately. The sponsors branch
    // README forbids a computed total on any surface (count x the minimum = a revenue floor), hence the
    // "and many more" chip rather than a number.
    private const int MaxSupporterChips = 30;
    // Three chip rows plus a sliver of the fourth, so the wall visibly continues below the fold.
    private const double SupporterWallMaxHeight = 100d;
    private const double ChipSpacing = 6d;
    // Softly rounded, not a full pill: about the corner ratio of a sponsor logo tile.
    private const double ChipCornerRadius = 6d;
    private const double MonthlyIconSize = 13d;
    private const int CountdownSeconds = 5;

    private readonly ILocalizationService _localization;
    private readonly ISponsorsService _sponsorsService;
    private readonly ILogService _logService;

    private bool _supportClicked;

    private SponsorsDialogMode _mode;
    private bool _countdownDone;
    private int _countdownRemaining = CountdownSeconds;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _countdownTimer;
    private ContentDialog _dialog = null!;
    private Action<string, bool> _setNoButton = null!;

    public SponsorsDialogBuilder(ILocalizationService localization, ISponsorsService sponsorsService, ILogService logService)
    {
        _localization = localization;
        _sponsorsService = sponsorsService;
        _logService = logService;
    }

    public ContentDialog Build(SponsorsDocument? data, SponsorsDialogMode mode)
    {
        _mode = mode;

        _dialog = new ContentDialog
        {
            PrimaryButtonText = _localization.GetString("Button_Yes"),
            SecondaryButtonText = _localization.GetString("Button_No"),
            DefaultButton = ContentDialogButton.Primary
        };
        _dialog.Resources["ContentDialogMaxWidth"] = DialogMaxWidth;
        _dialog.PrimaryButtonClick += (_, _) => OnSupportClicked();
        _setNoButton = (text, enabled) =>
        {
            _dialog.SecondaryButtonText = text;
            _dialog.IsSecondaryButtonEnabled = enabled;
        };

        ConfigureMode();

        _dialog.Content = BuildBody(data);
        return _dialog;
    }

    // The exit ask with no app window open: the dialog's body and buttons are the whole window, so no window
    // frame shows around a dialog. Same countdown on No as the ContentDialog.
    public FrameworkElement BuildExitWindow(SponsorsDocument? data, Action close)
    {
        _mode = SponsorsDialogMode.Exit;

        var yes = new Button
        {
            Content = _localization.GetString("Button_Yes"),
            Style = Named("AccentButtonStyle"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var no = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
        yes.Click += (_, _) =>
        {
            OnSupportClicked();
            close();
        };
        no.Click += (_, _) => close();
        _setNoButton = (text, enabled) =>
        {
            no.Content = text;
            no.IsEnabled = enabled;
        };
        _setNoButton(_localization.GetString("Support_NoCountdown", CountdownSeconds), false);

        var buttons = new Grid { ColumnSpacing = 8 };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(no, 1);
        buttons.Children.Add(yes);
        buttons.Children.Add(no);

        var body = new Border
        {
            Style = Named("CodeBuilt.DialogBody"),
            Padding = new Thickness(24),
            Child = BuildBody(data),
        };
        var root = new Grid { Width = DialogMaxWidth };
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var footer = new Border { Padding = new Thickness(24), Child = buttons };
        Grid.SetRow(footer, 1);
        root.Children.Add(body);
        root.Children.Add(footer);

        var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, e) =>
        {
            e.Handled = true;
            if (_countdownDone)
                close();
        };
        root.KeyboardAccelerators.Add(escape);
        root.Loaded += (_, _) =>
        {
            yes.Focus(FocusState.Programmatic);
            StartCountdown();
        };
        root.Unloaded += (_, _) => StopCountdown();
        return root;
    }

    // The header and the question stay put and only the walls scroll, so on a short screen the question still
    // sits right above Yes/No. ContentDialog wraps its content in its own ScrollViewer, which measures with
    // infinite height; with that one's vertical scrolling off, the dialog's max height reaches this Grid.
    private Grid BuildBody(SponsorsDocument? data)
    {
        var walls = new StackPanel { Spacing = 14 };
        var cards = SponsorRow.Pick(data?.Sponsors);
        if (cards.Count > 0)
            walls.Children.Add(BuildSponsorsSection(cards));
        walls.Children.Add(BuildSupportersCard(data));

        var scroller = new ScrollViewer
        {
            Content = walls,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled
        };
        var question = new TextBlock
        {
            Text = _localization.GetString("Support_Question"),
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        var root = new Grid { RowSpacing = 14 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(scroller, 1);
        Grid.SetRow(question, 2);
        root.Children.Add(BuildHeader());
        root.Children.Add(scroller);
        root.Children.Add(question);

        root.Loaded += (_, _) =>
        {
            if (root.FindAscendant<ScrollViewer>() is { Name: "ContentScrollViewer" } dialogScroller)
            {
                dialogScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                dialogScroller.VerticalScrollMode = ScrollMode.Disabled;
            }
        };
        return root;
    }

    private Grid BuildHeader()
    {
        var heart = new Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(26),
            Style = Named("CodeBuilt.HeartWash"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FluentIcons.WinUI.FluentIcon
            {
                Icon = FluentIcons.Common.Icon.Heart,
                IconVariant = FluentIcons.Common.IconVariant.Filled,
                FontSize = 26,
                Style = Named("CodeBuilt.HeartIcon"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var textPanel = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Support_Title"),
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Support_Message"),
            FontSize = 14,
            Style = Named("CodeBuilt.SecondaryText"),
            TextWrapping = TextWrapping.Wrap
        });

        // Grid (not horizontal StackPanel) so the text wraps in the remaining width.
        var header = new Grid { ColumnSpacing = 14 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(heart, 0);
        Grid.SetColumn(textPanel, 1);
        header.Children.Add(heart);
        header.Children.Add(textPanel);
        return header;
    }

    // Both walls open with the same header shape so the business/individual split reads at a glance.
    private Grid BuildSectionHeader(FluentIcons.Common.Icon icon, string titleKey, string subtitleKey, FrameworkElement? trailing)
    {
        var glyph = new FluentIcons.WinUI.FluentIcon
        {
            Icon = icon,
            FontSize = 18,
            Style = Named("CodeBuilt.SecondaryIcon"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = _localization.GetString(titleKey),
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        text.Children.Add(new TextBlock
        {
            Text = _localization.GetString(subtitleKey),
            FontSize = 12,
            Style = Named("CodeBuilt.SecondaryText"),
            TextWrapping = TextWrapping.Wrap
        });

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(glyph, 0);
        Grid.SetColumn(text, 1);
        header.Children.Add(glyph);
        header.Children.Add(text);

        if (trailing != null)
        {
            trailing.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(trailing, 2);
            header.Children.Add(trailing);
        }

        return header;
    }

    private StackPanel BuildSponsorsSection(List<SponsorEntry> cards)
    {
        var section = new StackPanel { Spacing = 8 };

        var becomeSponsor = new HyperlinkButton
        {
            Content = _localization.GetString("Support_BecomeSponsor"),
            NavigateUri = new Uri(BusinessSponsorUrl),
            FontSize = 12,
            Padding = new Thickness(6, 2, 6, 2)
        };
        section.Children.Add(BuildSectionHeader(FluentIcons.Common.Icon.Building, "Support_Sponsors", "Support_SponsorsSub", becomeSponsor));

        var wrap = new UniformWrapPanel
        {
            ItemWidth = CardWidth,
            ItemHeight = CardHeight,
            ColumnSpacing = CardColumnSpacing,
            RowSpacing = CardRowSpacing,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var sponsor in cards)
            wrap.Children.Add(BuildSponsorCard(sponsor));
        section.Children.Add(new ScrollViewer
        {
            Content = wrap,
            MaxHeight = SponsorWallMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled
        });

        section.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Support_Disclaimer"),
            FontSize = 11,
            Style = Named("CodeBuilt.TertiaryText"),
            TextWrapping = TextWrapping.Wrap
        });

        return section;
    }

    // Layers: the card fill, a tier-coloured wash fading down from the top, then the content.
    private Border BuildSponsorCard(SponsorEntry sponsor)
    {
        var content = new StackPanel
        {
            Spacing = 3,
            Margin = new Thickness(8, 5, 8, 8),
            VerticalAlignment = VerticalAlignment.Top
        };

        content.Children.Add(BuildTierLabel(sponsor));
        content.Children.Add(SponsorLogo.Build(_sponsorsService, sponsor, CardLogoSize));

        content.Children.Add(new TextBlock
        {
            Text = sponsor.Name,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0)
        });

        if (!string.IsNullOrEmpty(sponsor.Slogan))
        {
            content.Children.Add(new TextBlock
            {
                Text = sponsor.Slogan,
                FontSize = 11,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Style = Named("CodeBuilt.SecondaryText"),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        if (!string.IsNullOrEmpty(sponsor.City))
        {
            content.Children.Add(new TextBlock
            {
                Text = sponsor.City,
                FontSize = 11,
                Style = Named("CodeBuilt.SecondaryText"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        var urlLink = BuildUrlLink(sponsor.Url);
        if (urlLink != null)
            content.Children.Add(urlLink);

        double innerRadius = CardCornerRadius - 1;
        var layers = new Grid();
        layers.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(innerRadius),
            Style = Named(SponsorLogo.TierStyle(sponsor, "Wash"))
        });
        layers.Children.Add(content);

        return new Border
        {
            Style = Named(SponsorLogo.TierStyle(sponsor, "Card")),
            Child = layers,
            CornerRadius = new CornerRadius(CardCornerRadius)
        };
    }

    private static StackPanel BuildTierLabel(SponsorEntry sponsor)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        row.Children.Add(new FluentIcons.WinUI.FluentIcon
        {
            Icon = SponsorRow.IsEmerald(sponsor) ? FluentIcons.Common.Icon.Diamond : FluentIcons.Common.Icon.Star,
            IconVariant = FluentIcons.Common.IconVariant.Filled,
            FontSize = 11,
            Style = Named(SponsorLogo.TierStyle(sponsor, "Icon")),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock
        {
            Text = (sponsor.Tier ?? string.Empty).ToUpperInvariant(),
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CharacterSpacing = 120,
            Style = Named(SponsorLogo.TierStyle(sponsor, "Text")),
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    // Only for a non-empty absolute https URI; content is the host without a leading "www.".
    private static HyperlinkButton? BuildUrlLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return null;

        string host = uri.Host;
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host.Substring(4);

        return new HyperlinkButton
        {
            Content = host,
            NavigateUri = uri,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(0)
        };
    }

    private Border BuildSupportersCard(SponsorsDocument? data)
    {
        var supporters = data?.Supporters ?? new List<SupporterEntry>();
        var content = new StackPanel { Spacing = 10 };

        content.Children.Add(BuildSectionHeader(FluentIcons.Common.Icon.People, "Support_Supporters", "Support_SupportersSub", null));

        if (supporters.Count > 0)
        {
            var wall = new CommunityToolkit.WinUI.Controls.WrapPanel
            {
                HorizontalSpacing = ChipSpacing,
                VerticalSpacing = ChipSpacing
            };
            var shown = supporters.Take(MaxSupporterChips).ToList();
            foreach (var supporter in shown)
                wall.Children.Add(BuildSupporterChip(supporter));

            if (supporters.Count > MaxSupporterChips)
                wall.Children.Add(BuildChip(null, _localization.GetString("Support_ManyMore"), Named("CodeBuilt.TertiaryText"), null));

            content.Children.Add(new ScrollViewer
            {
                Content = wall,
                MaxHeight = SupporterWallMaxHeight,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Disabled
            });

            if (shown.Any(s => s.Monthly))
                content.Children.Add(BuildMonthlyKey());
        }

        content.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Support_HowToJoin"),
            FontSize = 12,
            Style = Named("CodeBuilt.SecondaryText"),
            TextWrapping = TextWrapping.Wrap
        });

        return new Border
        {
            Child = content,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Style = Named("CodeBuilt.CardSurface")
        };
    }

    // Matches the store and winhance.net: a renewal icon marks a monthly supporter, and the months they have
    // backed the developer show from the second month, when the count starts to mean something.
    private Border BuildSupporterChip(SupporterEntry supporter)
    {
        if (!supporter.Monthly)
            return BuildChip(null, supporter.Name, null, null);

        var months = supporter.Months > 1
            ? new TextBlock
            {
                Text = _localization.GetString("Support_MonthsShort", supporter.Months),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Style = Named("CodeBuilt.AccentText"),
                VerticalAlignment = VerticalAlignment.Center
            }
            : null;
        return BuildChip(BuildMonthlyIcon(), supporter.Name, null, months);
    }

    private static FluentIcons.WinUI.FluentIcon BuildMonthlyIcon() => new()
    {
        Icon = FluentIcons.Common.Icon.ArrowSync,
        FontSize = MonthlyIconSize,
        Style = Named("CodeBuilt.AccentIcon"),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, -3, 0)
    };

    private StackPanel BuildMonthlyKey()
    {
        var key = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        key.Children.Add(BuildMonthlyIcon());
        key.Children.Add(new TextBlock
        {
            Text = _localization.GetString("Support_MonthlyKey"),
            FontSize = 11,
            Style = Named("CodeBuilt.TertiaryText"),
            TextWrapping = TextWrapping.Wrap
        });
        return key;
    }

    // A null textStyle leaves the text on the dialog's own foreground.
    private static Border BuildChip(FrameworkElement? leading, string text, Style? textStyle, FrameworkElement? trailing)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (leading != null)
            row.Children.Add(leading);
        row.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            Style = textStyle,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        });
        if (trailing != null)
            row.Children.Add(trailing);

        return new Border
        {
            Style = Named("TechDetail.Chip"),
            CornerRadius = new CornerRadius(ChipCornerRadius),
            Padding = new Thickness(leading == null ? 10 : 8, 3, 10, 3),
            Child = row
        };
    }

    private void OnSupportClicked()
    {
        _supportClicked = true;
        string source = _mode == SponsorsDialogMode.Exit ? "app-exit" : "app";
        Windows.System.Launcher.LaunchUriAsync(new Uri(SupportUrl + source)).AsTask().FireAndForget(_logService);
    }

    private void ConfigureMode()
    {
        if (_mode == SponsorsDialogMode.Normal)
            return;

        // No is held back until the countdown elapses, so the ask is read before it is declined.
        // Yes closes straight away because _supportClicked is set before Closing runs.
        _setNoButton(_localization.GetString("Support_NoCountdown", CountdownSeconds), false);
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
                _setNoButton(_localization.GetString("Support_NoCountdown", _countdownRemaining), false);
            }
            else
            {
                _countdownDone = true;
                _setNoButton(_localization.GetString("Button_No"), true);
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

}
