using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winhance.UI.Features.Common.Models;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// A badge control that displays Windows version compatibility information on settings.
/// Shows a Windows icon with version number (10 or 11) and tooltip with details.
/// </summary>
public sealed partial class SettingBadge : UserControl
{
    public static readonly DependencyProperty BadgeTypeProperty =
        DependencyProperty.Register(
            nameof(BadgeType),
            typeof(BadgeType),
            typeof(SettingBadge),
            new PropertyMetadata(BadgeType.Win10, OnBadgeTypeChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(SettingBadge),
            new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty TooltipProperty =
        DependencyProperty.Register(
            nameof(Tooltip),
            typeof(string),
            typeof(SettingBadge),
            new PropertyMetadata(string.Empty));

    public BadgeType BadgeType
    {
        get => (BadgeType)GetValue(BadgeTypeProperty);
        set => SetValue(BadgeTypeProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Tooltip
    {
        get => (string)GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    public SettingBadge()
    {
        this.InitializeComponent();
        this.ActualThemeChanged += OnThemeChanged;
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyBadgeStyle();
    }

    private void OnThemeChanged(FrameworkElement sender, object args)
    {
        ApplyBadgeStyle();
    }

    private static void OnBadgeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingBadge badge)
        {
            badge.ApplyBadgeStyle();
        }
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingBadge badge)
        {
            badge.BadgeText.Text = e.NewValue?.ToString() ?? string.Empty;
        }
    }

    private void ApplyBadgeStyle()
    {
        var isDark = ActualTheme == ElementTheme.Dark;
        var suffix = isDark ? "Dark" : "Light";
        var prefix = BadgeType switch
        {
            BadgeType.Win11 => "Win11Badge",
            BadgeType.Win10 => "Win10Badge",
            BadgeType.WinBuild => "WinBuildBadge",
            _ => "Win10Badge"
        };

        // Apply resources
        if (Resources.TryGetValue($"{prefix}Background{suffix}", out var background))
        {
            BadgeBorder.Background = background as Brush;
        }

        if (Resources.TryGetValue($"{prefix}Border{suffix}", out var border))
        {
            BadgeBorder.BorderBrush = border as Brush;
        }

        if (Resources.TryGetValue($"{prefix}Foreground{suffix}", out var foreground))
        {
            var brush = foreground as Brush;
            BadgeText.Foreground = brush;
            BadgeIcon.Foreground = brush;
        }

        // Ensure text is updated
        BadgeText.Text = Text ?? string.Empty;
    }
}
