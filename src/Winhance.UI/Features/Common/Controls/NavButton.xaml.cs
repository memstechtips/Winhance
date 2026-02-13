using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Custom navigation button with icon-over-text layout, selection indicator,
/// loading overlay, and compact mode support.
/// </summary>
public sealed partial class NavButton : UserControl, INotifyPropertyChanged
{
    // Expanded dimensions
    private const double ExpandedWidth = 70;
    private const double ExpandedHeight = 60;

    // Compact dimensions (matching NavigationView items)
    private const double CompactWidth = 40;
    private const double CompactHeight = 40;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<NavButtonClickedEventArgs>? Clicked;

    #region Dependency Properties

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(
            nameof(IconGlyph),
            typeof(string),
            typeof(NavButton),
            new PropertyMetadata(null, OnIconPropertyChanged));

    public static readonly DependencyProperty IconPathProperty =
        DependencyProperty.Register(
            nameof(IconPath),
            typeof(string),
            typeof(NavButton),
            new PropertyMetadata(null, OnIconPropertyChanged));

    public static readonly DependencyProperty IconMarginProperty =
        DependencyProperty.Register(
            nameof(IconMargin),
            typeof(Thickness),
            typeof(NavButton),
            new PropertyMetadata(new Thickness(0)));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(NavButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(NavButton),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(NavButton),
            new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(
            nameof(IsCompact),
            typeof(bool),
            typeof(NavButton),
            new PropertyMetadata(false, OnIsCompactChanged));

    public static readonly DependencyProperty NavigationTagProperty =
        DependencyProperty.Register(
            nameof(NavigationTag),
            typeof(object),
            typeof(NavButton),
            new PropertyMetadata(null));

    public static readonly DependencyProperty BadgeValueProperty =
        DependencyProperty.Register(
            nameof(BadgeValue),
            typeof(int),
            typeof(NavButton),
            new PropertyMetadata(-1, OnBadgePropertyChanged));

    public static readonly DependencyProperty BadgeStatusProperty =
        DependencyProperty.Register(
            nameof(BadgeStatus),
            typeof(string),
            typeof(NavButton),
            new PropertyMetadata(string.Empty, OnBadgePropertyChanged));

    #endregion

    #region Properties

    /// <summary>
    /// The FontIcon glyph to display (e.g., "\uE71D").
    /// Use this for Segoe MDL2/Fluent font icons.
    /// </summary>
    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    /// <summary>
    /// The PathIcon geometry data to display (SVG path data string).
    /// Use this for Material Design icons or custom vector icons.
    /// </summary>
    public string? IconPath
    {
        get => (string?)GetValue(IconPathProperty);
        set => SetValue(IconPathProperty, value);
    }

    /// <summary>
    /// Optional margin for fine-tuning icon positioning.
    /// Use this to adjust icons that appear visually off-center.
    /// </summary>
    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    /// <summary>
    /// The text label displayed below the icon.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Whether this button is currently selected.
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Whether the button is in a loading state (shows spinner, blocks clicks).
    /// </summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>
    /// Whether the button should display in compact mode (icon only).
    /// </summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>
    /// Navigation identifier for this button.
    /// </summary>
    public object? NavigationTag
    {
        get => GetValue(NavigationTagProperty);
        set => SetValue(NavigationTagProperty, value);
    }

    /// <summary>
    /// Badge value to display. Set to -1 to hide the badge.
    /// </summary>
    public int BadgeValue
    {
        get => (int)GetValue(BadgeValueProperty);
        set => SetValue(BadgeValueProperty, value);
    }

    /// <summary>
    /// Badge status: "Attention", "Success", or "" (hidden).
    /// </summary>
    public string BadgeStatus
    {
        get => (string)GetValue(BadgeStatusProperty);
        set => SetValue(BadgeStatusProperty, value);
    }

    // Icon sizes (matching NavigationView)
    private const double ExpandedIconSize = 20;
    private const double CompactIconSize = 16;

    // Computed properties for bindings
    public double ActualButtonWidth => IsCompact ? CompactWidth : ExpandedWidth;
    public double ActualButtonHeight => IsCompact ? CompactHeight : ExpandedHeight;
    public double IconSize => IsCompact ? CompactIconSize : ExpandedIconSize;
    public Visibility TextVisibility => IsCompact ? Visibility.Collapsed : Visibility.Visible;
    public Visibility IndicatorVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    // Icon type visibility - show FontIcon if IconGlyph is set, PathIcon if IconPath is set
    public Visibility FontIconVisibility => !string.IsNullOrEmpty(IconGlyph) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PathIconVisibility => !string.IsNullOrEmpty(IconPath) && string.IsNullOrEmpty(IconGlyph) ? Visibility.Visible : Visibility.Collapsed;

    // Badge visibility
    public Visibility BadgeVisibility => BadgeValue >= 0 || BadgeStatus == "SuccessIcon" ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    private bool _isPointerOver;
    private bool _isFocused;

    public NavButton()
    {
        this.InitializeComponent();
        UpdateVisualState();

        // Keyboard and focus accessibility
        KeyDown += NavButton_KeyDown;
        GotFocus += NavButton_GotFocus;
        LostFocus += NavButton_LostFocus;
    }

    private void NavButton_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (IsLoading) return;

        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space)
        {
            Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
            e.Handled = true;
        }
    }

    private void NavButton_GotFocus(object sender, RoutedEventArgs e)
    {
        _isFocused = true;
        UpdateVisualState();
    }

    private void NavButton_LostFocus(object sender, RoutedEventArgs e)
    {
        _isFocused = false;
        UpdateVisualState();
    }

    #region Property Change Handlers

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton button)
        {
            button.NotifyPropertyChanged(nameof(IndicatorVisibility));
            button.UpdateVisualState();
        }
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton button)
        {
            button.NotifyPropertyChanged(nameof(LoadingVisibility));
            button.UpdateVisualState();
        }
    }

    private static void OnIsCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton button)
        {
            button.NotifyPropertyChanged(nameof(ActualButtonWidth));
            button.NotifyPropertyChanged(nameof(ActualButtonHeight));
            button.NotifyPropertyChanged(nameof(IconSize));
            button.NotifyPropertyChanged(nameof(TextVisibility));
        }
    }

    private static void OnBadgePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton button)
        {
            button.NotifyPropertyChanged(nameof(BadgeVisibility));
            button.ApplyBadgeStyle();
        }
    }

    private void ApplyBadgeStyle()
    {
        try
        {
            if (Badge == null) return;

            if (string.IsNullOrEmpty(BadgeStatus) || (BadgeValue < 0 && BadgeStatus != "SuccessIcon"))
            {
                Badge.Visibility = Visibility.Collapsed;
                return;
            }

            Badge.Visibility = Visibility.Visible;
            var styleKey = BadgeStatus switch
            {
                "Attention" => "AttentionValueInfoBadgeStyle",
                "Success" => "InformationalValueInfoBadgeStyle",
                "SuccessIcon" => "SuccessIconInfoBadgeStyle",
                _ => "AttentionValueInfoBadgeStyle"
            };

            if (Application.Current.Resources.TryGetValue(styleKey, out var style) && style is Style badgeStyle)
            {
                Badge.Style = badgeStyle;
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to apply badge style: {ex.Message}");
        }
    }

    private static void OnIconPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton button)
        {
            button.NotifyPropertyChanged(nameof(FontIconVisibility));
            button.NotifyPropertyChanged(nameof(PathIconVisibility));

            // Convert IconPath string to Geometry and apply to PathIcon
            if (e.Property == IconPathProperty && !string.IsNullOrEmpty(button.IconPath))
            {
                try
                {
                    var geometry = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                        typeof(Geometry), button.IconPath);
                    button.ButtonPathIcon.Data = geometry;
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to convert IconPath to Geometry: {ex.Message}");
                }
            }
        }
    }

    #endregion

    #region Pointer Events

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = true;
        UpdateVisualState();
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOver = false;
        UpdateVisualState();
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Block interaction when loading
        if (IsLoading) return;

        RootGrid.CapturePointer(e.Pointer);
    }

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        RootGrid.ReleasePointerCapture(e.Pointer);

        // Block interaction when loading
        if (IsLoading) return;

        // Only fire click if pointer is still over the button
        if (_isPointerOver)
        {
            Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
        }
    }

    #endregion

    #region Visual State Management

    private void UpdateVisualState()
    {
        // Determine background based on state
        if (IsSelected)
        {
            // Selected state: use tertiary fill
            BackgroundBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];
        }
        else if ((_isPointerOver || _isFocused) && !IsLoading)
        {
            // Hover/Focus state: use secondary fill
            BackgroundBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
        else
        {
            // Normal state: transparent
            BackgroundBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    #endregion

    private static void LogDebug(string message)
    {
        try { App.Services.GetService<ILogService>()?.LogDebug(message); } catch { }
    }

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Event args for NavButton click events.
/// </summary>
public class NavButtonClickedEventArgs : EventArgs
{
    public object? NavigationTag { get; }

    public NavButtonClickedEventArgs(object? navigationTag)
    {
        NavigationTag = navigationTag;
    }
}
