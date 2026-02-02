using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Collapsible navigation sidebar containing NavButton controls.
/// </summary>
public sealed partial class NavSidebar : UserControl, INotifyPropertyChanged
{
    // Sidebar dimensions (matching NavigationView defaults)
    private const double ExpandedWidth = 80;
    private const double CompactWidth = 48;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<NavButtonClickedEventArgs>? ItemClicked;

    private Dictionary<string, NavButton>? _navButtons;

    #region Dependency Properties

    public static readonly DependencyProperty IsPaneOpenProperty =
        DependencyProperty.Register(
            nameof(IsPaneOpen),
            typeof(bool),
            typeof(NavSidebar),
            new PropertyMetadata(true, OnIsPaneOpenChanged));

    public static readonly DependencyProperty SelectedTagProperty =
        DependencyProperty.Register(
            nameof(SelectedTag),
            typeof(string),
            typeof(NavSidebar),
            new PropertyMetadata(null, OnSelectedTagChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Whether the sidebar pane is open (expanded) or closed (compact).
    /// </summary>
    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    /// <summary>
    /// The currently selected navigation tag.
    /// </summary>
    public string? SelectedTag
    {
        get => (string?)GetValue(SelectedTagProperty);
        set => SetValue(SelectedTagProperty, value);
    }

    /// <summary>
    /// Computed property: true when pane is closed (compact mode).
    /// </summary>
    public bool IsCompact => !IsPaneOpen;

    /// <summary>
    /// Computed property: actual width based on pane state.
    /// </summary>
    public double ActualSidebarWidth => IsPaneOpen ? ExpandedWidth : CompactWidth;

    /// <summary>
    /// Computed property: padding for nav panels based on pane state.
    /// </summary>
    public Thickness NavPanelPadding => IsPaneOpen ? new Thickness(5, 0, 5, 0) : new Thickness(4, 0, 4, 0);

    #endregion

    public NavSidebar()
    {
        this.InitializeComponent();
        InitializeNavButtonDictionary();
    }

    private void InitializeNavButtonDictionary()
    {
        _navButtons = new Dictionary<string, NavButton>
        {
            { "SoftwareApps", SoftwareAppsButton },
            { "Optimize", OptimizeButton },
            { "Customize", CustomizeButton },
            { "AdvancedTools", AdvancedToolsButton },
            { "Settings", SettingsButton },
            { "More", MoreButton }
        };
    }

    #region Property Change Handlers

    private static void OnIsPaneOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavSidebar sidebar)
        {
            sidebar.NotifyPropertyChanged(nameof(IsCompact));
            sidebar.NotifyPropertyChanged(nameof(ActualSidebarWidth));
            sidebar.NotifyPropertyChanged(nameof(NavPanelPadding));
        }
    }

    private static void OnSelectedTagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavSidebar sidebar)
        {
            sidebar.UpdateSelectionState();
        }
    }

    #endregion

    #region Event Handlers

    private void NavButton_Clicked(object sender, NavButtonClickedEventArgs e)
    {
        var tag = e.NavigationTag?.ToString();
        if (!string.IsNullOrEmpty(tag))
        {
            SelectedTag = tag;
            ItemClicked?.Invoke(this, e);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Toggles the pane open/closed state.
    /// </summary>
    public void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    #endregion

    #region Selection Management

    private void UpdateSelectionState()
    {
        if (_navButtons == null) return;

        foreach (var kvp in _navButtons)
        {
            kvp.Value.IsSelected = kvp.Key == SelectedTag;
        }
    }

    /// <summary>
    /// Sets the loading state for a specific navigation button.
    /// </summary>
    /// <param name="tag">The navigation tag of the button.</param>
    /// <param name="isLoading">Whether the button should show loading state.</param>
    public void SetButtonLoading(string tag, bool isLoading)
    {
        if (_navButtons != null && _navButtons.TryGetValue(tag, out var button))
        {
            button.IsLoading = isLoading;
        }
    }

    /// <summary>
    /// Sets all buttons to loading or not loading state.
    /// </summary>
    /// <param name="isLoading">Whether all buttons should show loading state.</param>
    public void SetAllButtonsLoading(bool isLoading)
    {
        if (_navButtons == null) return;

        foreach (var button in _navButtons.Values)
        {
            button.IsLoading = isLoading;
        }
    }

    #endregion

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
