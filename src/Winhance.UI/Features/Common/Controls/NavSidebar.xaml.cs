using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.ViewModels;

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
    public event EventHandler? MoreMenuClosed;

    private Dictionary<string, NavButton>? _navButtons;
    private MoreMenuViewModel? _moreMenuViewModel;

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

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(MainWindowViewModel),
            typeof(NavSidebar),
            new PropertyMetadata(null));

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
    /// The ViewModel providing localized strings for nav buttons.
    /// </summary>
    public MainWindowViewModel? ViewModel
    {
        get => (MainWindowViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
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

        // Get MoreMenuViewModel and apply localized text to flyout after control is loaded
        this.Loaded += NavSidebar_Loaded;
    }

    private void NavSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        // Get MoreMenuViewModel for flyout commands and text
        _moreMenuViewModel = App.Services.GetService<MoreMenuViewModel>();

        // Apply localized text to More menu flyout items
        ApplyMoreMenuLocalizedText();

        // Subscribe to MoreMenuViewModel property changes to update flyout text
        if (_moreMenuViewModel != null)
        {
            _moreMenuViewModel.PropertyChanged += OnMoreMenuViewModelPropertyChanged;
        }

        // Subscribe to flyout closed event to restore selection
        if (MoreMenuFlyout != null)
        {
            MoreMenuFlyout.Closed += MoreMenuFlyout_Closed;
        }
    }

    private void OnMoreMenuViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Re-apply flyout text when MoreMenuViewModel properties change (language change)
        ApplyMoreMenuLocalizedText();
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

    /// <summary>
    /// Applies localized text to the More menu flyout items.
    /// </summary>
    private void ApplyMoreMenuLocalizedText()
    {
        if (_moreMenuViewModel == null || MoreMenuFlyout == null) return;

        foreach (var item in MoreMenuFlyout.Items)
        {
            if (item is MenuFlyoutItem menuItem)
            {
                var tag = menuItem.Tag as string;
                menuItem.Text = tag switch
                {
                    "ReportBug" => _moreMenuViewModel.MenuReportBug,
                    "CheckUpdates" => _moreMenuViewModel.MenuCheckForUpdates,
                    "OpenLogs" => _moreMenuViewModel.MenuWinhanceLogs,
                    "OpenScripts" => _moreMenuViewModel.MenuWinhanceScripts,
                    "CloseApp" => _moreMenuViewModel.MenuCloseWinhance,
                    _ => menuItem.Text
                };

                // Special case for version item (no tag, disabled)
                if (!menuItem.IsEnabled && string.IsNullOrEmpty(tag))
                {
                    menuItem.Text = _moreMenuViewModel.VersionInfo;
                }
            }
        }
    }

    /// <summary>
    /// Shows the More menu flyout positioned relative to the More button.
    /// Uses FlyoutBase.ShowAttachedFlyout which handles toggle behavior automatically.
    /// </summary>
    public void ShowMoreMenuFlyout()
    {
        try
        {
            FlyoutBase.ShowAttachedFlyout(MoreButton);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing More menu flyout: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the More menu flyout closing by raising an event for the parent to restore selection.
    /// </summary>
    private void MoreMenuFlyout_Closed(object? sender, object e)
    {
        // Raise event so MainWindow can restore selection based on current page
        MoreMenuClosed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles More menu item clicks and dispatches to MoreMenuViewModel commands.
    /// </summary>
    private void MoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string tag && _moreMenuViewModel != null)
        {
            switch (tag)
            {
                case "ReportBug":
                    _moreMenuViewModel.ReportBugCommand.Execute(null);
                    break;
                case "CheckUpdates":
                    _moreMenuViewModel.CheckForUpdatesCommand.Execute(null);
                    break;
                case "OpenLogs":
                    _moreMenuViewModel.OpenLogsCommand.Execute(null);
                    break;
                case "OpenScripts":
                    _moreMenuViewModel.OpenScriptsCommand.Execute(null);
                    break;
                case "CloseApp":
                    _moreMenuViewModel.CloseApplicationCommand.Execute(null);
                    break;
            }
        }
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

    /// <summary>
    /// Gets a NavButton by its tag.
    /// </summary>
    /// <param name="tag">The navigation tag of the button to get.</param>
    /// <returns>The NavButton if found, null otherwise.</returns>
    public NavButton? GetButton(string tag)
    {
        if (_navButtons != null && _navButtons.TryGetValue(tag, out var button))
        {
            return button;
        }
        return null;
    }

    #endregion

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
