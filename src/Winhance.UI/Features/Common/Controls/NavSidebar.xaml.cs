using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class NavSidebar : UserControl
{
    // Sidebar dimensions (matching NavigationView defaults)
    private const double ExpandedWidth = 80;
    private const double CompactWidth = 48;

    public event EventHandler<NavButtonClickedEventArgs>? ItemClicked;
    public event EventHandler? MoreMenuClosed;

    private Dictionary<string, NavButton>? _navButtons;
    private readonly MoreMenuViewModel _moreMenuViewModel = App.Services.GetRequiredService<MoreMenuViewModel>();
    private readonly ILogService _logService = App.Services.GetRequiredService<ILogService>();

    public static readonly DependencyProperty IsPaneOpenProperty =
        DependencyProperty.Register(
            nameof(IsPaneOpen),
            typeof(bool),
            typeof(NavSidebar),
            new PropertyMetadata(true));

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

    // The page the docs link opens for; MainWindow updates it on every navigation because SelectedTag
    // reads "More" while this flyout is open.
    public string? CurrentPageTag { get; set; }

    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    public string? SelectedTag
    {
        get => (string?)GetValue(SelectedTagProperty);
        set => SetValue(SelectedTagProperty, value);
    }

    public MainWindowViewModel? ViewModel
    {
        get => (MainWindowViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static double SidebarWidth(bool isPaneOpen) => isPaneOpen ? ExpandedWidth : CompactWidth;

    public static Thickness PanelPadding(bool isPaneOpen) => isPaneOpen ? new Thickness(5, 0, 5, 0) : new Thickness(4, 0, 4, 0);

    public NavSidebar()
    {
        this.InitializeComponent();
        InitializeNavButtonDictionary();

        this.Loaded += NavSidebar_Loaded;
        this.Unloaded += NavSidebar_Unloaded;
    }

    private void NavSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyMoreMenuLocalizedText();

        _moreMenuViewModel.PropertyChanged += OnMoreMenuViewModelPropertyChanged;

        if (MoreMenuFlyout != null)
        {
            MoreMenuFlyout.Closed += MoreMenuFlyout_Closed;
        }
    }

    // MoreMenuViewModel is a singleton that outlives the sidebar.
    private void NavSidebar_Unloaded(object sender, RoutedEventArgs e)
    {
        _moreMenuViewModel.PropertyChanged -= OnMoreMenuViewModelPropertyChanged;

        if (MoreMenuFlyout != null)
        {
            MoreMenuFlyout.Closed -= MoreMenuFlyout_Closed;
        }
    }

    private void OnMoreMenuViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyMoreMenuLocalizedText();
    }

    private void InitializeNavButtonDictionary()
    {
        _navButtons = new Dictionary<string, NavButton>
        {
            { "SoftwareApps", SoftwareAppsButton },
            { "Optimize", OptimizeButton },
            { "Customize", CustomizeButton },
            { "Autounattend", AutounattendButton },
            { "WimUtil", WimUtilButton },
            { "Settings", SettingsButton },
            { "More", MoreButton }
        };
    }

    private void ApplyMoreMenuLocalizedText()
    {
        if (MoreMenuFlyout == null) return;

        foreach (var item in MoreMenuFlyout.Items)
        {
            if (item is MenuFlyoutItem menuItem)
            {
                var tag = menuItem.Tag as string;
                menuItem.Text = tag switch
                {
                    "OpenDocs" => _moreMenuViewModel.MenuDocumentation,
                    "ReportBug" => _moreMenuViewModel.MenuReportBug,
                    "CheckUpdates" => _moreMenuViewModel.MenuCheckForUpdates,
                    "OpenLogs" => _moreMenuViewModel.MenuWinhanceLogs,
                    "OpenChangeHistory" => _moreMenuViewModel.MenuChangeHistory,
                    "OpenScripts" => _moreMenuViewModel.MenuWinhanceScripts,
                    "SupportWinhance" => _moreMenuViewModel.MenuSupportWinhance,
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

    public void ShowMoreMenuFlyout()
    {
        try
        {
            FlyoutBase.ShowAttachedFlyout(MoreButton);
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"Error showing More menu flyout: {ex.Message}");
        }
    }

    private void MoreMenuFlyout_Closed(object? sender, object e)
    {
        // The flyout's dismiss layer covered the button, so it never saw the pointer leave.
        MoreButton.ClearPointerOver();
        MoreMenuClosed?.Invoke(this, EventArgs.Empty);
    }

    private void MoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string tag)
        {
            switch (tag)
            {
                case "OpenDocs":
                    _moreMenuViewModel.OpenDocsCommand.Execute(CurrentPageTag);
                    break;
                case "ReportBug":
                    _moreMenuViewModel.ReportBugCommand.Execute(null);
                    break;
                case "CheckUpdates":
                    ViewModel?.UpdateCheck.CheckForUpdatesCommand.Execute(null);
                    break;
                case "OpenLogs":
                    _moreMenuViewModel.OpenLogsCommand.Execute(null);
                    break;
                case "OpenChangeHistory":
                    _moreMenuViewModel.OpenChangeHistoryCommand.Execute(null);
                    break;
                case "OpenScripts":
                    _moreMenuViewModel.OpenScriptsCommand.Execute(null);
                    break;
                case "SupportWinhance":
                    _moreMenuViewModel.SupportWinhanceCommand.Execute(null);
                    break;
                case "CloseApp":
                    _moreMenuViewModel.CloseApplicationCommand.Execute(null);
                    break;
            }
        }
    }

    private static void OnSelectedTagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavSidebar sidebar)
        {
            sidebar.UpdateSelectionState();
        }
    }

    private void NavButton_Clicked(object sender, NavButtonClickedEventArgs e)
    {
        var tag = e.NavigationTag?.ToString();
        if (!string.IsNullOrEmpty(tag))
        {
            SelectedTag = tag;
            ItemClicked?.Invoke(this, e);
        }
    }

    public void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    private void UpdateSelectionState()
    {
        if (_navButtons == null) return;

        foreach (var kvp in _navButtons)
        {
            kvp.Value.IsSelected = kvp.Key == SelectedTag;
        }
    }

    public NavButton? GetButton(string tag)
    {
        if (_navButtons != null && _navButtons.TryGetValue(tag, out var button))
        {
            return button;
        }
        return null;
    }

    public void SetButtonBadge(string tag, int value, string status)
    {
        if (_navButtons != null && _navButtons.TryGetValue(tag, out var button))
        {
            button.BadgeValue = value;
            button.BadgeStatus = status;
        }
    }

    public void ClearAllBadges()
    {
        if (_navButtons == null) return;

        foreach (var button in _navButtons.Values)
        {
            button.BadgeValue = -1;
            button.BadgeStatus = string.Empty;
        }
    }
}
