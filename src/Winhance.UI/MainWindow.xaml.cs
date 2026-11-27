using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Winhance.UI.Helpers;
using Winhance.UI.ViewModels;
using System.ComponentModel;
using Windows.Foundation;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI;

public sealed partial class MainWindow : Window
{
    private readonly IDispatcherService _dispatcherService = App.Services.GetRequiredService<IDispatcherService>();
    private WindowSizeManager? _windowSizeManager;

    private const double CompactPaneBelowWidth = 1008;
    private bool _isNarrowWindow;
    private bool _paneCompactedForWidth;
    private UiZoomManager? _uiZoomManager;
    private IDebounceTimer? _zoomIndicatorTimer;
    private ZoomIndicator? _zoomIndicator;
    private IConfigReviewService? _configReviewService;
    private IApplicationModeService? _applicationModeService;
    private INavBadgeService? _navBadgeService;
    private ILogService? _logService;
    private bool _isStartupLoading = true;

    private TaskProgressCoordinator? _taskProgressCoordinator;
    private NavigationRouter? _navigationRouter;
    private readonly NavHistory _navHistory = new();
    private MemoryDiagnostics? _memoryDiagnostics;
    private WinhanceMode _lastMode = WinhanceMode.Normal;
    private StartupUiCoordinator? _startupUiCoordinator;
    private TitleBarManager? _titleBarManager;
    private TrayController? _trayController;

    // Assigned after construction; Bindings.Update() refreshes x:Bind once it is set.
    public MainWindowViewModel? ViewModel { get; private set; }

    public MainWindow()
    {
        StartupLogger.Log("Constructor starting...");
        this.InitializeComponent();
        StartupLogger.Log("InitializeComponent completed");

        // Initialize DispatcherService - MUST be done before any service uses it
        InitializeDispatcherService();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        this.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
        WindowIcon.Apply(this.AppWindow);

        _titleBarManager = new TitleBarManager(this.AppWindow, _logService);
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;

        InitializeWindowSizeManager();

        InitializeTray();

        InitializeUiZoom();

        TrySetMicaBackdrop();

        InitializeFlowDirection();

        AppTitleBar.Loaded += AppTitleBar_Loaded;

        NavSidebar.Loaded += NavSidebar_Loaded;
        ContentFrame.Navigated += ContentFrame_Navigated;
        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootGrid_PointerPressed), handledEventsToo: true);
    }

    private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _memoryDiagnostics?.LogNavigation(e.Content as Page);
        // Every page drops elements on the way out, cached ones included (Optimize and Customize close their
        // open section), so every navigation releases them.
        NativeHeaps.ReleaseDroppedMemoryLater(_dispatcherService, _logService!);
        var tag = _navigationRouter?.GetTagForCurrentPage(e.SourcePageType);
        DocsButton.CommandParameter = tag;
        NavSidebar.CurrentPageTag = tag;
        _navHistory.Arrived(tag);
        BackButton.IsEnabled = _navHistory.CanGoBack;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => GoBackAsync().FireAndForget(_logService!);

    private void BackAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        GoBackAsync().FireAndForget(_logService!);
        args.Handled = true;
    }

    // The mouse's back button (XButton1), which most apps treat as Back.
    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(RootGrid).Properties.IsXButton1Pressed) return;
        GoBackAsync().FireAndForget(_logService!);
        e.Handled = true;
    }

    private async Task GoBackAsync()
    {
        if (_navHistory.BeginBack() is not { } tag) return;
        try
        {
            await OpenPageAsync(tag);
        }
        finally
        {
            _navHistory.EndBack();
            BackButton.IsEnabled = _navHistory.CanGoBack;
        }
    }

    private void NavSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        StartupLogger.Log("NavSidebar_Loaded");
        NavSidebar.MoreMenuClosed += NavSidebar_MoreMenuClosed;

        // Skip auto-navigation during startup -- CompleteStartup() will trigger it
        if (_isStartupLoading)
        {
            StartupLogger.Log("Startup loading in progress, deferring navigation");
            return;
        }

        NavSidebar.SelectedTag = "SoftwareApps";
        _navigationRouter?.NavigateToPage(ContentFrame, "SoftwareApps", applyNavBadges: ApplyNavBadges);
        StartupLogger.Log("SoftwareApps selected");
    }

    // Called by App.xaml.cs after Activate + InitializeTheme.
    public void StartStartupOperations()
    {
        StartupLogger.Log("StartStartupOperations called");

        _startupUiCoordinator = new StartupUiCoordinator(_dispatcherService, _logService!);
        _startupUiCoordinator.InitializeLoadingOverlay(
            LoadingTitleText, LoadingTaglineText, LoadingStatusText);
        _startupUiCoordinator.ShowLoadingSponsors(LoadingContent);

        _startupUiCoordinator.RunStartupAndCompleteAsync(
            LoadingStatusText, ContentFrame, NavSidebar, LoadingOverlay, () => ViewModel,
            markStartupComplete: () => _isStartupLoading = false).FireAndForget(_logService!);
    }

    private void InitializeWindowSizeManager()
    {
        try
        {
            var userPreferencesService = App.Services.GetRequiredService<IUserPreferencesService>();
            _logService = App.Services.GetRequiredService<ILogService>();
            _windowSizeManager = new WindowSizeManager(this.AppWindow, userPreferencesService, _logService);
            _windowSizeManager.Initialize();

            var applicationCloseService = App.Services.GetRequiredService<IApplicationCloseService>();
            applicationCloseService.BeforeShutdown = async () =>
            {
                _memoryDiagnostics?.LogExit();
                _trayController?.RemoveIcon();
                if (_windowSizeManager != null)
                    await _windowSizeManager.SaveWindowSettingsAsync();
            };

            this.AppWindow.Closing += AppWindow_Closing;
        }
        catch (Exception ex)
        {
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
            _logService?.LogDebug($"Failed to initialize WindowSizeManager: {ex.Message}");
        }
    }

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        if (_trayController?.TryHideOnClose() == true)
            return;
        App.Services.GetRequiredService<IApplicationCloseService>().CheckOperationsAndCloseAsync().FireAndForget(_logService!);
    }

    private void InitializeTray()
    {
        try
        {
            _trayController = App.Services.GetRequiredService<TrayController>();
            _trayController.Attach(this, ShowAppUpdates);
        }
        catch (Exception ex)
        {
            _logService?.LogError("Failed to set up the tray icon", ex);
        }
    }

    private void ShowAppUpdates()
    {
        NavSidebar.SelectedTag = "SoftwareApps";
        _navigationRouter?.NavigateToPage(ContentFrame, "SoftwareApps", applyNavBadges: ApplyNavBadges);
        var softwareApps = App.Services.GetRequiredService<SoftwareAppsViewModel>();
        softwareApps.ShowAppUpdates();
    }

    // The OemPlus/OemMinus accelerators XAML can't express, and Ctrl+MouseWheel.
    private void InitializeUiZoom()
    {
        try
        {
            var prefs = App.Services.GetRequiredService<IUserPreferencesService>();
            _logService ??= App.Services.GetRequiredService<ILogService>();
            _uiZoomManager = new UiZoomManager(ZoomViewport, ZoomHost, prefs, _logService);

            // Main-row +/- keys (OemPlus=187, OemMinus=189) — not expressible in XAML.
            RootGrid.KeyboardAccelerators.Add(MakeZoomAccelerator((VirtualKey)187, ZoomInAccelerator_Invoked));
            RootGrid.KeyboardAccelerators.Add(MakeZoomAccelerator((VirtualKey)189, ZoomOutAccelerator_Invoked));

            // Ctrl+MouseWheel, caught even if an inner ScrollViewer already handled the wheel.
            ZoomViewport.AddHandler(
                UIElement.PointerWheelChangedEvent,
                new PointerEventHandler(ZoomViewport_PointerWheelChanged),
                handledEventsToo: true);

            _uiZoomManager.Initialize();
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to initialize UI zoom: {ex.Message}");
        }
    }

    private KeyboardAccelerator MakeZoomAccelerator(
        VirtualKey key,
        TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> handler)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = VirtualKeyModifiers.Control };
        accelerator.Invoked += handler;
        return accelerator;
    }

    // Mica on Windows 11, DesktopAcrylic fallback on Windows 10.
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

    private void InitializeFlowDirection()
    {
        try
        {
            var localizationService = App.Services.GetRequiredService<ILocalizationService>();
            ApplyFlowDirection(localizationService.IsRightToLeft);
            localizationService.LanguageChanged += LocalizationService_LanguageChanged;
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to initialize FlowDirection: {ex.Message}");
        }
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e) =>
        _dispatcherService.Enqueue(() => ApplyFlowDirection(App.Services.GetRequiredService<ILocalizationService>().IsRightToLeft));

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
        _titleBarManager?.ApplyThemeToCaptionButtons(RootGrid.ActualTheme);
    }

    private void RootGrid_ActualThemeChanged(FrameworkElement sender, object args) =>
        _titleBarManager?.ApplyThemeToCaptionButtons(RootGrid.ActualTheme);

    private void InitializeDispatcherService()
    {
        try
        {
            if (_dispatcherService is DispatcherService concreteService)
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

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        _titleBarManager?.SetTitleBarPadding(
            LeftPaddingColumn, RightPaddingColumn, AppTitleBar, RootGrid.FlowDirection);

        // Defer passthrough region setup to ensure all elements are laid out
        _dispatcherService.Enqueue(() =>
            _titleBarManager?.SetPassthroughRegions(AppTitleBar, NavButtons, TitleBarButtons, ModeSwitcher));

        FitTitleBar();
        FitNavPane(AppTitleBar.ActualWidth);
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        TitleBarButtons.SizeChanged += TitleBarButtons_SizeChanged;
        // A language switch changes the labels' length without resizing the window.
        ModeSwitcher.SizeChanged += ModeSwitcher_SizeChanged;
        // A Grid whose columns overflow reports their width, not the window's, and stops raising SizeChanged as the
        // window narrows further, so the title bar and the mode bars are fitted to the window content instead.
        RootGrid.SizeChanged += RootGrid_SizeChanged;

        InitializeViewModel();
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        FitTitleBar();
        FitNavPane(args.NewSize.Width);
        _titleBarManager?.SetPassthroughRegions(AppTitleBar, NavButtons, TitleBarButtons, ModeSwitcher);
    }

    private void TitleBarButtons_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        FitTitleBar();
        _titleBarManager?.SetPassthroughRegions(AppTitleBar, NavButtons, TitleBarButtons, ModeSwitcher);
    }

    private void ModeSwitcher_SizeChanged(object sender, SizeChangedEventArgs args) => FitTitleBar();

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        FitTitleBar();
        FitModeBars();
    }

    private void InitializeViewModel()
    {
        try
        {
            ViewModel = App.Services.GetRequiredService<MainWindowViewModel>();

            WindowsFilterButton.Command = ViewModel.ToggleWindowsFilterCommand;
            DonateButton.Command = ViewModel.DonateCommand;
            BugReportButton.Command = ViewModel.BugReportCommand;
            DocsButton.Command = ViewModel.DocsCommand;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.UpdateCheck.PropertyChanged += UpdateCheck_PropertyChanged;
            ViewModel.ReviewModeBar.PropertyChanged += ReviewModeBar_PropertyChanged;
            ViewModel.BuilderModeBar.PropertyChanged += BuilderModeBar_PropertyChanged;

            ViewModel.Initialize();

            UpdateAppIcon();

            if (App.Services.GetRequiredService<IVersionService>().GetCurrentVersion().IsBeta)
            {
                var banner = new TextBlock
                {
                    Text = "Release-Preview: Version is for Testing Only",
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xE7, 0x4C, 0x3C)),
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0),
                    IsHitTestVisible = false,
                };
                Grid.SetColumn(banner, 4);
                AppTitleBar.Children.Add(banner);
            }

            NavSidebar.ViewModel = ViewModel;

            _taskProgressCoordinator = new TaskProgressCoordinator(
                TaskProgressControl, TaskProgressControl2, TaskProgressControl3,
                App.Services.GetRequiredService<ITaskProgressService>(), _dispatcherService);

            TaskProgressControl.CancelCommand = ViewModel.TaskProgress.CancelCommand;
            TaskProgressControl.CancelText = ViewModel.TaskProgress.CancelButtonLabel;
            TaskProgressControl.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;
            TaskProgressControl2.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;
            TaskProgressControl3.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;

            // Pending Explorer restart bar. The pending state changes on background apply threads,
            // so the ViewModel's notification is marshalled onto the dispatcher before Refresh()
            // touches any bound property.
            PendingRestartBar.ViewModel = App.Services.GetRequiredService<PendingRestartViewModel>();

            ViewModel.TaskProgress.PropertyChanged += TaskProgress_PropertyChanged;
            ViewModel.TaskProgress.ScriptProgressReceived += TaskProgress_ScriptProgressReceived;

            _configReviewService = App.Services.GetRequiredService<IConfigReviewService>();
            _navBadgeService = App.Services.GetRequiredService<INavBadgeService>();
            _navigationRouter = new NavigationRouter(
                _configReviewService, _navBadgeService, _dispatcherService, new PageCurtain(ContentFrame, PageLoader, _logService!));

            _configReviewService.ReviewModeChanged += OnReviewModeBadgeChanged;
            _configReviewService.BadgeStateChanged += OnBadgeStateChanged;

            _memoryDiagnostics = App.Services.GetRequiredService<MemoryDiagnostics>();
            _memoryDiagnostics.Attach(this);
            _memoryDiagnostics.Log("startup");

            _applicationModeService = App.Services.GetRequiredService<IApplicationModeService>();
            _applicationModeService.ModeChanged += OnApplicationModeChangedForNavLocks;
            LeaveUnavailablePage();

            App.Services.GetRequiredService<IEventBus>().Subscribe<PageRequestedEvent>(request =>
                _dispatcherService.Enqueue(() => OpenRequestedPageAsync(request).FireAndForget(_logService!)));

            ViewModel.LoadFilterPreference();

            // Notify x:Bind that ViewModel is now available
            Bindings.Update();
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to initialize ViewModel: {ex.Message}");
        }
    }

    private void TaskProgress_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is { } vm)
            _taskProgressCoordinator?.HandlePropertyChanged(vm.TaskProgress, e.PropertyName);
    }

    private void TaskProgress_ScriptProgressReceived(int slotIndex, TaskProgressDetail detail) =>
        _taskProgressCoordinator?.HandleScriptProgressReceived(slotIndex, detail);

    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _paneCompactedForWidth = false;
        NavSidebar.TogglePane();
    }

    // The mode labels go first, since each mode button keeps its icon and tooltip, then the app title, then the
    // Support text.
    private void FitTitleBar()
    {
        UIElement[] modeLabels = [ModeLabelText, NormalModeText, BuilderModeText, ConfigReviewModeText];
        FitToWidth.Apply(
            RootGrid.ActualWidth,
            TitleBarContentWidth,
            reset: () => FitToWidth.SetVisible(true, [.. modeLabels, AppTitleStack, SupportButtonText]),
            () => FitToWidth.SetVisible(false, modeLabels),
            () => FitToWidth.SetVisible(false, AppTitleStack),
            () => FitToWidth.SetVisible(false, SupportButtonText));
    }

    private ReviewModeBar? _reviewModeBar;
    private BuilderModeBar? _builderModeBar;
    private OtsElevationBanner? _otsElevationBanner;
    private UpdateBanner? _updateBanner;

    private void FitModeBars()
    {
        _builderModeBar?.Fit(RootGrid.ActualWidth);
        _reviewModeBar?.Fit(RootGrid.ActualWidth);
    }

    // The mode bars and banners are built when they show and dropped when they hide. x:Load cannot do this in a
    // Window: the generated code calls FindName, which a Window does not have.
    private T? ShowPart<T>(T? part, bool show, int row, Func<T> create, Panel? host = null) where T : FrameworkElement
    {
        host ??= RootGrid;
        if (!show)
        {
            if (part is not null)
                host.Children.Remove(part);
            return null;
        }

        if (part is not null)
            return part;

        part = create();
        Grid.SetRow(part, row);
        host.Children.Add(part);
        return part;
    }

    private T FitsToWindow<T>(T bar) where T : FrameworkElement
    {
        bar.SizeChanged += ModeBar_SizeChanged;
        return bar;
    }

    private void ModeBar_SizeChanged(object sender, SizeChangedEventArgs args) => FitModeBars();

    private double TitleBarContentWidth()
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double width = LeftPaddingColumn.ActualWidth + RightPaddingColumn.ActualWidth;
        foreach (var element in new FrameworkElement[] { NavButtons, AppIcon, AppTitleStack, ModeSwitcher, TitleBarButtons })
        {
            element.Measure(unbounded);
            width += element.DesiredSize.Width;
        }

        return width;
    }

    // Below Microsoft's 1008px large-window breakpoint the pane gives its width to the page. It comes back when the
    // window widens again, unless the user has toggled it in between.
    private void FitNavPane(double windowWidth)
    {
        bool narrow = windowWidth < CompactPaneBelowWidth;
        if (narrow == _isNarrowWindow)
            return;

        _isNarrowWindow = narrow;
        if (narrow && NavSidebar.IsPaneOpen)
        {
            NavSidebar.IsPaneOpen = false;
            _paneCompactedForWidth = true;
        }
        else if (!narrow && _paneCompactedForWidth)
        {
            NavSidebar.IsPaneOpen = true;
            _paneCompactedForWidth = false;
        }
    }

    private void NavSidebar_ItemClicked(object sender, NavButtonClickedEventArgs e)
    {
        var tag = e.NavigationTag?.ToString();
        StartupLogger.Log($"NavSidebar_ItemClicked - Tag: {tag}");

        if (tag == "More")
        {
            NavSidebar.ShowMoreMenuFlyout();
            return;
        }

        OpenPageAsync(tag).FireAndForget(_logService!);
    }

    // Unattend and WIMUtil are never locked: the view model asks before changing mode, and a no keeps the current page.
    private async Task<bool> OpenPageAsync(string? tag)
    {
        if (tag is null || ViewModel is not { } viewModel || !await viewModel.RequestPageAsync(tag))
        {
            SelectCurrentPage();
            return false;
        }

        NavSidebar.SelectedTag = tag;
        if (_navigationRouter is { } router)
            await router.NavigateToPage(ContentFrame, tag, applyNavBadges: ApplyNavBadges);
        return true;
    }

    private async Task OpenRequestedPageAsync(PageRequestedEvent request)
    {
        if (!await OpenPageAsync(request.PageTag) || request.PageTag != "SoftwareApps")
            return;

        var softwareApps = App.Services.GetRequiredService<SoftwareAppsViewModel>();
        if (request.FeatureId == FeatureIds.ExternalApps)
            softwareApps.IsExternalAppsTabSelected = true;
        else if (request.FeatureId == FeatureIds.WindowsApps)
            softwareApps.IsWindowsAppsTabSelected = true;
    }

    private void NavSidebar_MoreMenuClosed(object? sender, EventArgs e) => SelectCurrentPage();

    private void SelectCurrentPage()
    {
        var currentTag = _navigationRouter?.GetTagForCurrentPage(ContentFrame.CurrentSourcePageType);
        if (!string.IsNullOrEmpty(currentTag))
        {
            NavSidebar.SelectedTag = currentTag;
        }
    }

    private void OnReviewModeBadgeChanged(object? sender, EventArgs e)
    {
        _dispatcherService.Enqueue(() =>
        {
            if (_configReviewService?.IsInReviewMode == true)
            {
                _navBadgeService?.SubscribeToSoftwareAppsChanges(() =>
                    _dispatcherService.Enqueue(ApplyNavBadges));
                ApplyNavBadges();

                LeaveUnavailablePage();
            }
            else
            {
                NavSidebar.ClearAllBadges();
                _navBadgeService?.UnsubscribeFromSoftwareAppsChanges();

                LeaveUnavailablePage();
            }

            NativeHeaps.ReleaseDroppedMemoryLater(_dispatcherService, _logService!);
        });
    }

    private void OnBadgeStateChanged(object? sender, EventArgs e)
    {
        _dispatcherService.Enqueue(ApplyNavBadges);
    }

    private void OnApplicationModeChangedForNavLocks(object? sender, EventArgs e)
    {
        var mode = _applicationModeService?.CurrentMode ?? WinhanceMode.Normal;
        var builderOpened = mode == WinhanceMode.Builder && _lastMode != WinhanceMode.Builder;
        _lastMode = mode;
        _dispatcherService.Enqueue(LeaveUnavailablePage);
        // Every card swaps its mode-specific parts.
        NativeHeaps.ReleaseDroppedMemoryLater(_dispatcherService, _logService!);
        if (builderOpened && App.Services.GetRequiredService<IBuilderModeEntry>().EditingProfile is not null)
            _dispatcherService.Enqueue(LeaveSettingsPage);
    }

    // Editing a profile in Builder starts from Settings, which has nothing to build.
    private void LeaveSettingsPage()
    {
        if (_navigationRouter is not { } router || router.GetTagForCurrentPage(ContentFrame.CurrentSourcePageType) != "Settings")
            return;
        router.NavigateToPage(ContentFrame, "Optimize", applyNavBadges: ApplyNavBadges);
        NavSidebar.SelectedTag = "Optimize";
    }

    // A mode change can leave the frame on a page the new mode does not offer.
    private void LeaveUnavailablePage()
    {
        var mode = _applicationModeService?.CurrentMode ?? WinhanceMode.Normal;
        var target = _applicationModeService?.CurrentBuilderTarget ?? BuilderTarget.Config;
        var currentTag = _navigationRouter?.GetTagForCurrentPage(ContentFrame.CurrentSourcePageType);
        if (NavLockPolicy.GateFor(currentTag, mode, target) == PageGate.Open)
            return;

        _navigationRouter?.NavigateToPage(ContentFrame, "SoftwareApps", applyNavBadges: ApplyNavBadges);
        NavSidebar.SelectedTag = "SoftwareApps";
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

    // Only what genuinely needs code-behind: BitmapImage creation, opacity, and the Narrator
    // announcement; the rest is x:Bind.
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.AppIconSource))
        {
            _dispatcherService.Enqueue(UpdateAppIcon);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowsFilterTooltip) && ViewModel != null)
        {
            _dispatcherService.Enqueue(() =>
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
        else if (e.PropertyName == nameof(MainWindowViewModel.IsOtsInfoBarOpen) && ViewModel is { } vm)
        {
            _dispatcherService.Enqueue(() => _otsElevationBanner = ShowPart(
                _otsElevationBanner, vm.IsOtsInfoBarOpen, 2, () => new OtsElevationBanner(vm)));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsWindowsFilterButtonEnabled) && ViewModel != null)
        {
            _dispatcherService.Enqueue(() =>
            {
                WindowsFilterIcon.Opacity = ViewModel.IsWindowsFilterButtonEnabled ? 1.0 : 0.4;
            });
        }
    }

    private void UpdateCheck_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } vm || e.PropertyName != nameof(UpdateCheckViewModel.IsUpdateInfoBarOpen))
            return;

        _dispatcherService.Enqueue(() => _updateBanner = ShowPart(
            _updateBanner, vm.UpdateCheck.IsUpdateInfoBarOpen, 3, () => new UpdateBanner(vm.UpdateCheck)));
    }

    // Narrator announcements and building or dropping the bar; the rest is x:Bind.
    private void ReviewModeBar_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;
        var rm = ViewModel.ReviewModeBar;

        if (e.PropertyName == nameof(ReviewModeBarViewModel.IsInReviewMode))
        {
            _dispatcherService.Enqueue(() =>
            {
                _reviewModeBar = ShowPart(_reviewModeBar, rm.IsInReviewMode, 1, () => FitsToWindow(new ReviewModeBar(rm)));
                UIElement source = (UIElement?)_reviewModeBar ?? RootGrid;
                var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(source)
                           ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(source);
                peer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    rm.IsInReviewMode ? $"{rm.ReviewModeTitleText}. {rm.ReviewModeDescriptionText}" : "Config Review Mode ended",
                    rm.IsInReviewMode ? "ReviewModeEntered" : "ReviewModeExited");
            });
        }
    }

    private void UpdateAppIcon()
    {
        try
        {
            if (ViewModel != null)
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.DecodePixelWidth = 40;
                bitmapImage.DecodePixelHeight = 40;
                bitmapImage.DecodePixelType = DecodePixelType.Logical;
                bitmapImage.UriSource = new Uri(ViewModel.AppIconSource);
                AppIcon.Source = bitmapImage;
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to update app icon: {ex.Message}");
        }
    }

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null || sender is not ToggleButton tb || tb.Tag is not string tag)
            return;

        WinhanceMode? target = tag switch
        {
            "Normal" => WinhanceMode.Normal,
            "Builder" => WinhanceMode.Builder,
            "ConfigReview" => WinhanceMode.ConfigReview,
            _ => null
        };

        if (target == null)
            return;

        ViewModel.RequestSwitchModeAsync(target.Value).FireAndForget(_logService!);
    }

    private void BuilderModeBar_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } vm || e.PropertyName != nameof(BuilderModeBarViewModel.IsBuilderActive))
            return;

        _dispatcherService.Enqueue(() => _builderModeBar = ShowPart(
            _builderModeBar, vm.BuilderModeBar.IsBuilderActive, 1, () => FitsToWindow(new BuilderModeBar(vm.BuilderModeBar, _logService!))));
    }

    private void NavigateAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var tag = NavAccelerators.TagFor(sender.Key);

        if (tag != null)
        {
            OpenPageAndFocusAsync(tag).FireAndForget(_logService!);

            args.Handled = true;
        }
    }

    private async Task OpenPageAndFocusAsync(string tag)
    {
        // Focus the NavButton so Narrator announces the page name
        if (await OpenPageAsync(tag))
            NavSidebar.GetButton(tag)?.Focus(FocusState.Keyboard);
    }

    private void ZoomInAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _uiZoomManager?.StepUp();
        ShowZoomIndicator();
        args.Handled = true;
    }

    private void ZoomOutAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _uiZoomManager?.StepDown();
        ShowZoomIndicator();
        args.Handled = true;
    }

    private void ZoomResetAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _uiZoomManager?.Reset();
        ShowZoomIndicator();
        args.Handled = true;
    }

    private void ZoomViewport_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
            return;

        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta > 0)
            _uiZoomManager?.StepUp();
        else if (delta < 0)
            _uiZoomManager?.StepDown();

        ShowZoomIndicator();
        e.Handled = true;
    }

    // Shown on every zoom key, even at a limit, so the user sees why nothing changed (#692).
    private void ShowZoomIndicator()
    {
        if (_uiZoomManager is null)
            return;

        var percent = (int)Math.Round(_uiZoomManager.Factor * 100);
        _zoomIndicator = ShowPart(_zoomIndicator, true, 0, () => new ZoomIndicator(), ZoomViewport);
        _zoomIndicator!.SetText(App.Services.GetRequiredService<ILocalizationService>()
            .GetString("Zoom_Indicator_Format", percent));

        _zoomIndicatorTimer ??= _dispatcherService.CreateDebounceTimer(TimeSpan.FromMilliseconds(1500), HideZoomIndicator);
        _zoomIndicatorTimer.Restart();
    }

    private void HideZoomIndicator() =>
        _zoomIndicator = ShowPart(_zoomIndicator, false, 0, () => new ZoomIndicator(), ZoomViewport);

    private void MoreMenuAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        NavSidebar.ShowMoreMenuFlyout();
        args.Handled = true;
    }

    private void TitleBarAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var button = sender.Key switch
        {
            VirtualKey.Number1 => DonateButton,
            VirtualKey.Number2 => WindowsFilterButton,
            VirtualKey.Number3 => BugReportButton,
            VirtualKey.Number4 => DocsButton,
            _ => (Button?)null
        };

        if (button == null)
        {
            args.Handled = true;
            return;
        }

        if (button.Command?.CanExecute(button.CommandParameter) == true)
        {
            button.Command.Execute(button.CommandParameter);
        }

        // Announce after a short delay so async state changes are reflected
        _dispatcherService.Enqueue(() =>
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
}
