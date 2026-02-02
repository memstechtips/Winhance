using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
            new PropertyMetadata("\uE700"));

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

    #endregion

    #region Properties

    /// <summary>
    /// The FontIcon glyph to display (e.g., "\uE71D").
    /// </summary>
    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
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

    #endregion

    private bool _isPointerOver;

    public NavButton()
    {
        this.InitializeComponent();
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
        else if (_isPointerOver && !IsLoading)
        {
            // Hover state: use secondary fill
            BackgroundBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        }
        else
        {
            // Normal state: transparent
            BackgroundBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    #endregion

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
