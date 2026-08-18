using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

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
    private ILogService? _logService;

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

    public bool IsCompact => !IsPaneOpen;

    public double ActualSidebarWidth => IsPaneOpen ? ExpandedWidth : CompactWidth;

    public Thickness NavPanelPadding => IsPaneOpen ? new Thickness(5, 0, 5, 0) : new Thickness(4, 0, 4, 0);

    public NavSidebar()
    {
        this.InitializeComponent();
        InitializeNavButtonDictionary();

        this.Loaded += NavSidebar_Loaded;
    }

    private void NavSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        _moreMenuViewModel = App.Services.GetService<MoreMenuViewModel>();
        _logService = App.Services.GetService<ILogService>();

        ApplyMoreMenuLocalizedText();

        if (_moreMenuViewModel != null)
        {
            _moreMenuViewModel.PropertyChanged += OnMoreMenuViewModelPropertyChanged;
        }

        if (MoreMenuFlyout != null)
        {
            MoreMenuFlyout.Closed += MoreMenuFlyout_Closed;
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
            { "AdvancedTools", AdvancedToolsButton },
            { "Settings", SettingsButton },
            { "More", MoreButton }
        };
    }

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
            _logService?.LogDebug($"Error showing More menu flyout: {ex.Message}");
        }
    }

    private void MoreMenuFlyout_Closed(object? sender, object e)
    {
        // Raise event so MainWindow can restore selection based on current page
        MoreMenuClosed?.Invoke(this, EventArgs.Empty);
    }

    private void MoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string tag && _moreMenuViewModel != null)
        {
            switch (tag)
            {
                case "OpenDocs":
                    _moreMenuViewModel.OpenDocsCommand.Execute(null);
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

    public void SetButtonLocked(string tag, bool isLocked, string? tooltip = null)
    {
        if (_navButtons != null && _navButtons.TryGetValue(tag, out var button))
        {
            button.IsLocked = isLocked;
            if (isLocked && !string.IsNullOrEmpty(tooltip))
            {
                ToolTipService.SetToolTip(button, tooltip);
            }
            else if (!isLocked)
            {
                ToolTipService.SetToolTip(button, null);
            }
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

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
