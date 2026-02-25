using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Helpers;
using Winhance.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Winhance.UI;

/// <summary>
/// Main application window with custom NavSidebar navigation.
/// Delegates task progress, navigation, startup, and title bar management to helper classes.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private MainWindowViewModel? _viewModel;
    private WindowSizeManager? _windowSizeManager;
    private IConfigReviewService? _configReviewService;
    private INavBadgeService? _navBadgeService;
    private ILogService? _logService;
    private bool _isStartupLoading = true;

    // Helper classes (Phases 2-5)
    private TaskProgressCoordinator? _taskProgressCoordinator;
    private NavigationRouter? _navigationRouter;
    private StartupUiCoordinator? _startupUiCoordinator;
    private TitleBarManager? _titleBarManager;

    /// <summary>
    /// ViewModel exposed for x:Bind in XAML. Raises PropertyChanged so bindings update
    /// when the ViewModel is assigned after construction.
    /// </summary>
    public MainWindowViewModel? ViewModel
    {
        get => _viewModel;
        private set
        {
            if (_viewModel != value)
            {
                _viewModel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewModel)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        StartupLogger.Log("MainWindow", "Constructor starting...");
        this.InitializeComponent();
        StartupLogger.Log("MainWindow", "InitializeComponent completed");

        // Extend content into title bar for custom title bar experience
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Set tall title bar mode so caption buttons fill the full height
        this.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;

        // Create TitleBarManager and apply theme-aware caption button colors
        _titleBarManager = new TitleBarManager(this.AppWindow, _logService);
        RootGrid.ActualThemeChanged += (_, _) => _titleBarManager.ApplyThemeToCaptionButtons(RootGrid.ActualTheme);
        RootGrid.Loaded += (_, _) => _titleBarManager.ApplyThemeToCaptionButtons(RootGrid.ActualTheme);

        // Initialize window size manager for position/size persistence
        InitializeWindowSizeManager();

        // Apply Mica backdrop (Windows 11) with fallback to DesktopAcrylic (Windows 10)
        TrySetMicaBackdrop();

        // Initialize DispatcherService - MUST be done before any service uses it
        InitializeDispatcherService();

        // Apply initial FlowDirection for RTL languages and subscribe to language changes
        InitializeFlowDirection();

        // Set up title bar after loaded
        AppTitleBar.Loaded += AppTitleBar_Loaded;

        // Set default navigation after sidebar is loaded
        NavSidebar.Loaded += NavSidebar_Loaded;
    }

    #region Startup & Initialization

    /// <summary>
    /// Sets the default navigation item after the NavSidebar is loaded.
    /// </summary>
    private void NavSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        StartupLogger.Log("MainWindow", "NavSidebar_Loaded");
        NavSidebar.MoreMenuClosed += NavSidebar_MoreMenuClosed;

        // Skip auto-navigation during startup -- CompleteStartup() will trigger it
        if (_isStartupLoading)
        {
            StartupLogger.Log("MainWindow", "Startup loading in progress, deferring navigation");
            return;
        }

        NavSidebar.SelectedTag = "SoftwareApps";
        _navigationRouter?.NavigateToPage(ContentFrame, "SoftwareApps", applyNavBadges: ApplyNavBadges);
        StartupLogger.Log("MainWindow", "SoftwareApps selected");
    }

    /// <summary>
    /// Kicks off the async startup sequence. Called by App.xaml.cs after Activate + InitializeTheme.
    /// </summary>
    public void StartStartupOperations()
    {
        StartupLogger.Log("MainWindow", "StartStartupOperations called");

        _startupUiCoordinator = new StartupUiCoordinator(this.DispatcherQueue, _logService);
        _startupUiCoordinator.InitializeLoadingOverlay(
            LoadingTitleText, LoadingTaglineText, LoadingStatusText, LoadingLogo, RootGrid);

        _ = _startupUiCoordinator.RunStartupAndCompleteAsync(
            LoadingStatusText, ContentFrame, NavSidebar, LoadingOverlay, ViewModel,
            markStartupComplete: () => _isStartupLoading = false);
    }

    /// <summary>
    /// Initializes the WindowSizeManager for window position/size persistence
    /// and wires up ApplicationCloseService for proper shutdown with donation dialog.
    /// </summary>
    private void InitializeWindowSizeManager()
    {
        try
        {
            var userPreferencesService = App.Services.GetRequiredService<IUserPreferencesService>();
            _logService = App.Services.GetRequiredService<ILogService>();
            _windowSizeManager = new WindowSizeManager(this.AppWindow, userPreferencesService, _logService);
            _windowSizeManager.InitializeAsync().FireAndForget(_logService!);

            var applicationCloseService = App.Services.GetRequiredService<IApplicationCloseService>();
            applicationCloseService.BeforeShutdown = async () =>
            {
                if (_windowSizeManager != null)
                    await _windowSizeManager.SaveWindowSettingsAsync();
            };

            this.AppWindow.Closing += async (sender, args) =>
            {
                args.Cancel = true;
                await applicationCloseService.CheckOperationsAndCloseAsync();
            };
        }
        catch (Exception ex)
        {
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
            _logService?.LogDebug($"Failed to initialize WindowSizeManager: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to set Mica backdrop for Windows 11, falls back to DesktopAcrylic for Windows 10.
    /// </summary>
    private void TrySetMicaBackdrop()
    {
        try
        {
            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
                return;
            }
            if (DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to set backdrop: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the initial FlowDirection based on the current language and subscribes to language changes.
    /// </summary>
    private void InitializeFlowDirection()
    {
        try
        {
            var localizationService = App.Services.GetService<ILocalizationService>();
            if (localizationService != null)
            {
                ApplyFlowDirection(localizationService.IsRightToLeft);
                localizationService.LanguageChanged += (_, _) =>
                {
                    DispatcherQueue.TryEnqueue(() => ApplyFlowDirection(localizationService.IsRightToLeft));
                };
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to initialize FlowDirection: {ex.Message}");
        }
    }

    private void ApplyFlowDirection(bool isRightToLeft)
    {
        RootGrid.FlowDirection = isRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        if (AppTitleBar.IsLoaded)
        {
            _titleBarManager?.SetTitleBarPadding(
                LeftPaddingColumn, RightPaddingColumn, AppTitleBar, RootGrid.FlowDirection);
        }
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeDialogService();
    }

    private void InitializeDispatcherService()
    {
        try
        {
            var dispatcherService = App.Services.GetRequiredService<IDispatcherService>();
            if (dispatcherService is DispatcherService concreteService)
            {
                concreteService.Initialize(this.DispatcherQueue);
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to initialize DispatcherService: {ex.Message}");
        }
    }

    private void InitializeDialogService()
    {
        try
        {
            var dialogService = App.Services.GetRequiredService<IDialogService>();
            if (dialogService is DialogService concreteService)
            {
                concreteService.XamlRoot = RootGrid.XamlRoot;
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to initialize DialogService: {ex.Message}");
        }
    }

    #endregion

    #region Title Bar

    /// <summary>
    /// Called when the title bar is loaded. Sets up ViewModel bindings and padding.
    /// </summary>
    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        _titleBarManager?.SetTitleBarPadding(
            LeftPaddingColumn, RightPaddingColumn, AppTitleBar, RootGrid.FlowDirection);

        // Defer passthrough region setup to ensure all elements are laid out
        DispatcherQueue.TryEnqueue(() =>
            _titleBarManager?.SetPassthroughRegions(AppTitleBar, PaneToggleButton, TitleBarButtons));

        AppTitleBar.SizeChanged += (_, _) =>
            _titleBarManager?.SetPassthroughRegions(AppTitleBar, PaneToggleButton, TitleBarButtons);
        TitleBarButtons.SizeChanged += (_, _) =>
            _titleBarManager?.SetPassthroughRegions(AppTitleBar, PaneToggleButton, TitleBarButtons);

        // Initialize ViewModel and wire up bindings
        InitializeViewModel();
    }

    /// <summary>
    /// Initializes the ViewModel and wires up button commands and helper classes.
    /// </summary>
    private void InitializeViewModel()
    {
        try
        {
            ViewModel = App.Services.GetService<MainWindowViewModel>();

            if (ViewModel != null)
            {
                // Wire up button commands
                SaveConfigButton.Command = ViewModel.SaveConfigCommand;
                ImportConfigButton.Command = ViewModel.ImportConfigCommand;
                WindowsFilterButton.Command = ViewModel.ToggleWindowsFilterCommand;
                DonateButton.Command = ViewModel.DonateCommand;
                BugReportButton.Command = ViewModel.BugReportCommand;
                DocsButton.Command = ViewModel.DocsCommand;

                // Set initial filter button icon
                UpdateFilterButtonIcon();

                // Subscribe to property changes that require code-behind
                // (Narrator announcements, icon conversion, dynamic button creation, filter icon opacity)
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                ViewModel.UpdateCheck.PropertyChanged += UpdateCheck_PropertyChanged;
                ViewModel.ReviewModeBar.PropertyChanged += ReviewModeBar_PropertyChanged;

                // Deferred initialization: subscribes to events and sets initial state
                ViewModel.Initialize();

                // Set initial icon
                UpdateAppIcon();

                // Show beta banner if this is a beta build
                var versionService = App.Services.GetService<IVersionService>();
                if (versionService?.GetCurrentVersion().IsBeta == true)
                {
                    BetaBannerText.Visibility = Visibility.Visible;
                }

                // Pass ViewModel to NavSidebar for localized nav button text
                NavSidebar.ViewModel = ViewModel;

                // Wire up Task Progress Coordinator (Phase 2)
                _taskProgressCoordinator = new TaskProgressCoordinator(
                    TaskProgressControl, TaskProgressControl2, TaskProgressControl3,
                    _logService!, this.DispatcherQueue);

                TaskProgressControl.CancelCommand = ViewModel.TaskProgress.CancelCommand;
                TaskProgressControl.CancelText = ViewModel.TaskProgress.CancelButtonLabel;
                TaskProgressControl.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;
                TaskProgressControl2.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;
                TaskProgressControl3.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;

                ViewModel.TaskProgress.PropertyChanged += (s, e) =>
                    _taskProgressCoordinator?.HandlePropertyChanged(ViewModel.TaskProgress, e.PropertyName);
                ViewModel.TaskProgress.ScriptProgressReceived +=
                    (slotIndex, detail) => _taskProgressCoordinator?.HandleScriptProgressReceived(slotIndex, detail);

                // Wire up Navigation Router (Phase 3)
                _configReviewService = App.Services.GetService<IConfigReviewService>();
                _navBadgeService = App.Services.GetService<INavBadgeService>();
                _navigationRouter = new NavigationRouter(
                    _configReviewService, _navBadgeService, this.DispatcherQueue);

                // Subscribe to review mode badge events
                if (_configReviewService != null)
                {
                    _configReviewService.ReviewModeChanged += OnReviewModeBadgeChanged;
                    _configReviewService.BadgeStateChanged += OnBadgeStateChanged;
                }

                // Load filter preference asynchronously
                _ = ViewModel.LoadFilterPreferenceAsync();

                // Notify x:Bind that ViewModel is now available
                Bindings.Update();
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to initialize ViewModel: {ex.Message}");
        }
    }

    #endregion

    #region Navigation

    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        NavSidebar.TogglePane();
    }

    private void NavSidebar_ItemClicked(object sender, NavButtonClickedEventArgs e)
    {
        var tag = e.NavigationTag?.ToString();
        StartupLogger.Log("MainWindow", $"NavSidebar_ItemClicked - Tag: {tag}");

        if (tag == "More")
        {
            NavSidebar.ShowMoreMenuFlyout();
            return;
        }

        _navigationRouter?.NavigateToPage(ContentFrame, tag, applyNavBadges: ApplyNavBadges);
    }

    private void NavSidebar_MoreMenuClosed(object? sender, EventArgs e)
    {
        var currentTag = _navigationRouter?.GetTagForCurrentPage(ContentFrame.CurrentSourcePageType);
        if (!string.IsNullOrEmpty(currentTag))
        {
            NavSidebar.SelectedTag = currentTag;
        }
    }

    #endregion

    #region Review Mode Badges

    private void OnReviewModeBadgeChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_configReviewService?.IsInReviewMode == true)
            {
                _navBadgeService?.SubscribeToSoftwareAppsChanges(() =>
                    DispatcherQueue.TryEnqueue(ApplyNavBadges));
                ApplyNavBadges();
            }
            else
            {
                NavSidebar.ClearAllBadges();
                _navBadgeService?.UnsubscribeFromSoftwareAppsChanges();
            }
        });
    }

    private void OnBadgeStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ApplyNavBadges);
    }

    private void ApplyNavBadges()
    {
        if (_navBadgeService == null) return;
        var badges = _navBadgeService.ComputeNavBadges();
        foreach (var badge in badges)
        {
            NavSidebar.SetButtonBadge(badge.Tag, badge.Count, badge.Style);
        }
    }

    #endregion

    #region PropertyChanged Handlers (code-behind only -- Narrator, icon conversion, dynamic buttons)

    /// <summary>
    /// Handles MainWindowViewModel property changes that genuinely require code-behind:
    /// icon source (BitmapImage creation), filter icon (geometry conversion), filter icon opacity,
    /// and WindowsFilterTooltip Narrator announcement.
    /// Tooltip text, InfoBar, AppTitle/AppSubtitle are now handled by XAML x:Bind.
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.AppIconSource))
        {
            DispatcherQueue.TryEnqueue(UpdateAppIcon);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowsFilterTooltip) && ViewModel != null)
        {
            // Narrator announcement for filter state change (visual update handled by XAML binding)
            DispatcherQueue.TryEnqueue(() =>
            {
                var tooltip = ViewModel.WindowsFilterTooltip;
                var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(WindowsFilterButton)
                           ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(WindowsFilterButton);
                peer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    tooltip,
                    "FilterStateChanged");
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowsFilterIcon) && ViewModel != null)
        {
            DispatcherQueue.TryEnqueue(UpdateFilterButtonIcon);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsWindowsFilterButtonEnabled) && ViewModel != null)
        {
            // Update opacity for the disabled state (IsEnabled is handled by XAML binding)
            DispatcherQueue.TryEnqueue(() =>
            {
                WindowsFilterIcon.Opacity = ViewModel.IsWindowsFilterButtonEnabled ? 1.0 : 0.4;
            });
        }
    }

    /// <summary>
    /// Handles UpdateCheckViewModel property changes that require code-behind:
    /// dynamic action button creation (cannot be done in XAML).
    /// IsOpen, Title, Message, Severity are now handled by XAML x:Bind.
    /// </summary>
    private void UpdateCheck_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.PropertyName == nameof(UpdateCheckViewModel.IsUpdateActionButtonVisible)
            || e.PropertyName == nameof(UpdateCheckViewModel.InstallNowButtonText))
        {
            DispatcherQueue.TryEnqueue(UpdateInfoBarActionButton);
        }
    }

    /// <summary>
    /// Handles ReviewModeBarViewModel property changes that require code-behind:
    /// Narrator announcements for review mode entry/exit, and ReviewModeBar visibility toggle.
    /// Text bindings, IsEnabled, AutomationProperties.Name are now handled by XAML x:Bind.
    /// </summary>
    private void ReviewModeBar_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;
        var rm = ViewModel.ReviewModeBar;

        if (e.PropertyName == nameof(ReviewModeBarViewModel.IsInReviewMode))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ReviewModeBar.Visibility = rm.IsInReviewMode ? Visibility.Visible : Visibility.Collapsed;
                if (rm.IsInReviewMode)
                {
                    // Announce review mode entry to Narrator
                    var announcement = $"{rm.ReviewModeTitleText}. {rm.ReviewModeDescriptionText}";
                    var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(ReviewModeBar)
                               ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(ReviewModeBar);
                    peer?.RaiseNotificationEvent(
                        Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                        Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                        announcement,
                        "ReviewModeEntered");
                }
                else
                {
                    // Announce review mode exit to Narrator
                    var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(RootGrid)
                               ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(RootGrid);
                    peer?.RaiseNotificationEvent(
                        Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                        Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                        "Config Review Mode ended",
                        "ReviewModeExited");
                }
            });
        }
    }

    #endregion

    #region UI Update Helpers

    private void UpdateFilterButtonIcon()
    {
        try
        {
            if (ViewModel != null && WindowsFilterIcon != null)
            {
                var geometry = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Geometry),
                    ViewModel.WindowsFilterIcon);
                WindowsFilterIcon.Data = geometry;
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to update filter button icon: {ex.Message}");
        }
    }

    private void UpdateInfoBarActionButton()
    {
        if (ViewModel == null) return;
        var uc = ViewModel.UpdateCheck;

        if (uc.IsUpdateActionButtonVisible)
        {
            var button = new Button
            {
                Content = uc.InstallNowButtonText,
                Command = uc.InstallUpdateCommand,
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            };
            UpdateInfoBar.ActionButton = button;
        }
        else
        {
            UpdateInfoBar.ActionButton = null;
        }
    }

    private void UpdateAppIcon()
    {
        try
        {
            if (ViewModel != null)
            {
                AppIcon.Source = new BitmapImage(new Uri(ViewModel.AppIconSource));
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to update app icon: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers (XAML-referenced)

    private void OtsElevationInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel?.DismissOtsInfoBar();
    }

    private void UpdateInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel?.UpdateCheck.DismissUpdateInfoBar();
    }

    private void ReviewModeApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ReviewModeBar.ApplyReviewedConfigCommand.CanExecute(null) == true)
            ViewModel.ReviewModeBar.ApplyReviewedConfigCommand.Execute(null);
    }

    private void ReviewModeCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ReviewModeBar.CancelReviewModeCommand.CanExecute(null) == true)
            ViewModel.ReviewModeBar.CancelReviewModeCommand.Execute(null);
    }

    #endregion

    #region Keyboard Accelerators

    private void NavigateAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var tag = sender.Key switch
        {
            VirtualKey.Number1 => "SoftwareApps",
            VirtualKey.Number2 => "Optimize",
            VirtualKey.Number3 => "Customize",
            VirtualKey.Number4 => "AdvancedTools",
            VirtualKey.Number5 => "Settings",
            _ => null
        };

        if (tag != null)
        {
            NavSidebar.SelectedTag = tag;
            _navigationRouter?.NavigateToPage(ContentFrame, tag, applyNavBadges: ApplyNavBadges);

            // Focus the NavButton so Narrator announces the page name
            var navButton = NavSidebar.GetButton(tag);
            navButton?.Focus(FocusState.Keyboard);

            args.Handled = true;
        }
    }

    private void SaveConfigAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel?.SaveConfigCommand.CanExecute(null) == true)
        {
            ViewModel.SaveConfigCommand.Execute(null);
        }
        args.Handled = true;
    }

    private void MoreMenuAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        NavSidebar.ShowMoreMenuFlyout();
        args.Handled = true;
    }

    private void TitleBarAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var button = sender.Key switch
        {
            VirtualKey.Number1 => SaveConfigButton,
            VirtualKey.Number2 => ImportConfigButton,
            VirtualKey.Number3 => WindowsFilterButton,
            VirtualKey.Number4 => DonateButton,
            VirtualKey.Number5 => BugReportButton,
            VirtualKey.Number6 => DocsButton,
            _ => (Button?)null
        };

        if (button == null)
        {
            args.Handled = true;
            return;
        }

        if (button.Command?.CanExecute(null) == true)
        {
            button.Command.Execute(null);
        }

        // Announce after a short delay so async state changes are reflected
        DispatcherQueue.TryEnqueue(() =>
        {
            var name = AutomationProperties.GetName(button);
            if (!string.IsNullOrEmpty(name))
            {
                var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(button)
                           ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(button);
                peer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    name,
                    "TitleBarAction");
            }
        });

        args.Handled = true;
    }

    #endregion
}
