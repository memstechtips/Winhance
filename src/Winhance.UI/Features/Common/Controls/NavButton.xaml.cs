using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class NavButton : UserControl
{
    private const double ExpandedWidth = 70;
    private const double ExpandedHeight = 60;

    // Compact dimensions (matching NavigationView items)
    private const double CompactWidth = 40;
    private const double CompactHeight = 40;

    public event EventHandler<NavButtonClickedEventArgs>? Clicked;

    public static readonly DependencyProperty IconSymbolProperty =
        DependencyProperty.Register(
            nameof(IconSymbol),
            typeof(string),
            typeof(NavButton),
            new PropertyMetadata(null));

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
            new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(NavButton),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(
            nameof(IsCompact),
            typeof(bool),
            typeof(NavButton),
            new PropertyMetadata(false));

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
            new PropertyMetadata(-1));

    public static readonly DependencyProperty BadgeStatusProperty =
        DependencyProperty.Register(
            nameof(BadgeStatus),
            typeof(string),
            typeof(NavButton),
            new PropertyMetadata(string.Empty));

    public string? IconSymbol
    {
        get => (string?)GetValue(IconSymbolProperty);
        set => SetValue(IconSymbolProperty, value);
    }

    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public object? NavigationTag
    {
        get => GetValue(NavigationTagProperty);
        set => SetValue(NavigationTagProperty, value);
    }

    // -1 hides the badge.
    public int BadgeValue
    {
        get => (int)GetValue(BadgeValueProperty);
        set => SetValue(BadgeValueProperty, value);
    }

    // "Attention", "Success", or "" (hidden).
    public string BadgeStatus
    {
        get => (string)GetValue(BadgeStatusProperty);
        set => SetValue(BadgeStatusProperty, value);
    }

    // Icon sizes (matching NavigationView)
    private const double ExpandedIconSize = 20;
    private const double CompactIconSize = 16;
    private const double LabelFontSize = 10;

    public static double ButtonWidth(bool isCompact) => isCompact ? CompactWidth : ExpandedWidth;

    public static double ButtonHeight(bool isCompact) => isCompact ? CompactHeight : ExpandedHeight;

    public static double IconSizeFor(bool isCompact) => isCompact ? CompactIconSize : ExpandedIconSize;

    public static bool HasIcon(string? iconSymbol) => !string.IsNullOrEmpty(iconSymbol);

    public static FluentIcons.Common.Icon IconKindOf(string? iconSymbol) =>
        Enum.TryParse<FluentIcons.Common.Icon>(iconSymbol, ignoreCase: true, out var icon) ? icon : default;

    public static bool HasBadge(int badgeValue, string badgeStatus) =>
        !string.IsNullOrEmpty(badgeStatus) && (badgeValue >= 0 || badgeStatus == "SuccessIcon");

    public static Style? BadgeStyleFor(string badgeStatus)
    {
        var key = badgeStatus switch
        {
            "Attention" => "AttentionValueInfoBadgeStyle",
            "Success" => "InformationalValueInfoBadgeStyle",
            "SuccessIcon" => "SuccessIconInfoBadgeStyle",
            _ => "AttentionValueInfoBadgeStyle"
        };
        return Application.Current.Resources.TryGetValue(key, out var style) ? style as Style : null;
    }

    private bool _isPointerOver;

    // Closing a popup replays the last pointer update, so PointerEntered can fire under a pointer that never moved
    // and no PointerExited follows. Set when the More flyout closes; hover then waits for a real move or a press.
    private bool _hoverWaitsForMove;

    public NavButton()
    {
        this.InitializeComponent();
        UpdateVisualState();

        KeyDown += NavButton_KeyDown;
    }

    private void NavButton_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space)
        {
            Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
        => new NavButtonAutomationPeer(this);

    // Lets the automation peer route Narrator's Invoke through the same gates as pointer / keyboard.
    internal void InvokeFromAutomation()
    {
        Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavButton button)
        {
            button.UpdateVisualState();
        }
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((NavButton)d).FitLabel();

    private void ButtonText_Loaded(object sender, RoutedEventArgs e) => FitLabel();

    // The label wraps only between words, so a word wider than the label ("Einstellungen") steps the font down
    // until it fits instead of breaking mid-word. Past the last step it is trimmed with an ellipsis.
    private void FitLabel()
    {
        if (ButtonText is not { } label)
            return;

        FitToWidth.Apply(
            label.MaxWidth,
            () => FitToWidth.WidestWord(Text, word => WordWidth(label, word)),
            reset: () => label.FontSize = LabelFontSize,
            () => label.FontSize = 9,
            () => label.FontSize = 8,
            () => label.FontSize = 7);
    }

    private static double WordWidth(TextBlock label, string word)
    {
        var probe = new TextBlock { Text = word, FontSize = label.FontSize, FontFamily = label.FontFamily, FontWeight = label.FontWeight };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_hoverWaitsForMove)
            return;

        _isPointerOver = true;
        UpdateVisualState();
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_hoverWaitsForMove)
            return;

        _hoverWaitsForMove = false;
        _isPointerOver = true;
        UpdateVisualState();
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoverWaitsForMove = false;
        _isPointerOver = false;
        UpdateVisualState();
    }

    // Focus on press, as Button does, so a flyout this button opens hands focus back to it on Esc.
    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _hoverWaitsForMove = false;
        _isPointerOver = true;
        UpdateVisualState();
        Focus(FocusState.Pointer);
        RootGrid.CapturePointer(e.Pointer);
    }

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        RootGrid.ReleasePointerCapture(e.Pointer);

        if (_isPointerOver)
        {
            Clicked?.Invoke(this, new NavButtonClickedEventArgs(NavigationTag));
        }
    }

    internal void ClearPointerOver()
    {
        _isPointerOver = false;
        _hoverWaitsForMove = true;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        string state = IsSelected ? "Selected"
            : _isPointerOver ? "PointerOver"
            : "Normal";
        VisualStateManager.GoToState(this, state, false);
    }
}

public class NavButtonClickedEventArgs : EventArgs
{
    public object? NavigationTag { get; }

    public NavButtonClickedEventArgs(object? navigationTag)
    {
        NavigationTag = navigationTag;
    }
}

public sealed partial class NavButtonAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
    public NavButtonAutomationPeer(NavButton owner) : base(owner) { }

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Button;

    protected override string GetClassNameCore() => nameof(NavButton);

    protected override object GetPatternCore(PatternInterface patternInterface)
        => patternInterface == PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);

    public void Invoke()
    {
        if (Owner is NavButton navButton)
        {
            navButton.InvokeFromAutomation();
        }
    }
}
