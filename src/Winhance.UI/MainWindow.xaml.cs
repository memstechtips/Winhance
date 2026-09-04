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
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Helpers;
using Winhance.UI.ViewModels;
using System.ComponentModel;
using Windows.Foundation;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private MainWindowViewModel? _viewModel;
    private WindowSizeManager? _windowSizeManager;
    private UiZoomManager? _uiZoomManager;
    private IConfigReviewService? _configReviewService;
    private INavBadgeService? _navBadgeService;
    private ILogService? _logService;
    private PendingRestartViewModel? _pendingRestartViewModel;
    private bool _isStartupLoading = true;

    private TaskProgressCoordinator? _taskProgressCoordinator;
    private NavigationRouter? _navigationRouter;
    private StartupUiCoordinator? _startupUiCoordinator;
    private TitleBarManager? _titleBarManager;

    // Raises PropertyChanged so bindings update when the ViewModel is assigned after construction.
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
        StartupLogger.Log("Constructor starting...");
        this.InitializeComponent();
        StartupLogger.Log("InitializeComponent completed");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        this.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;

        _titleBarManager = new TitleBarManager(this.AppWindow, _logService);
        RootGrid.ActualThemeChanged += (_, _) => _titleBarManager.ApplyThemeToCaptionButtons(RootGrid.ActualTheme);
        RootGrid.Loaded += (_, _) => _titleBarManager.ApplyThemeToCaptionButtons(RootGrid.ActualTheme);

        InitializeWindowSizeManager();

        InitializeUiZoom();

        TrySetMicaBackdrop();

        // Initialize DispatcherService - MUST be done before any service uses it
        InitializeDispatcherService();

        InitializeFlowDirection();

        AppTitleBar.Loaded += AppTitleBar_Loaded;

        NavSidebar.Loaded += NavSidebar_Loaded;
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

        _startupUiCoordinator = new StartupUiCoordinator(this.DispatcherQueue, _logService);
        _startupUiCoordinator.InitializeLoadingOverlay(
            LoadingTitleText, LoadingTaglineText, LoadingStatusText, LoadingLogo, RootGrid);

        _ = _startupUiCoordinator.RunStartupAndCompleteAsync(
            LoadingStatusText, ContentFrame, NavSidebar, LoadingOverlay, () => ViewModel,
            markStartupComplete: () => _isStartupLoading = false);
    }

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
            var localizationService = App.Services.GetService<ILocalizationService>();
            if (localizationService != null)
            {
                ApplyFlowDirection(localizationService.IsRightToLeft);
                localizationService.LanguageChanged += (_, _) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ApplyFlowDirection(localizationService.IsRightToLeft);

                        var advancedToolsButton = NavSidebar.GetButton("AdvancedTools");
                        if (advancedToolsButton?.IsLocked == true)
                        {
                            ToolTipService.SetToolTip(advancedToolsButton,
                                localizationService.GetStringOrDefault("Nav_AdvancedTools_Locked_Tooltip", "Unavailable during config review"));
                        }
                    });
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

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        _titleBarManager?.SetTitleBarPadding(
            LeftPaddingColumn, RightPaddingColumn, AppTitleBar, RootGrid.FlowDirection);

        // Defer passthrough region setup to ensure all elements are laid out
        DispatcherQueue.TryEnqueue(() =>
            _titleBarManager?.SetPassthroughRegions(AppTitleBar, PaneToggleButton, TitleBarButtons, ModeSwitcher));

        AppTitleBar.SizeChanged += (_, _) =>
            _titleBarManager?.SetPassthroughRegions(AppTitleBar, PaneToggleButton, TitleBarButtons, ModeSwitcher);
        TitleBarButtons.SizeChanged += (_, _) =>
            _titleBarManager?.SetPassthroughRegions(AppTitleBar, PaneToggleButton, TitleBarButtons, ModeSwitcher);

        InitializeViewModel();
    }

    private void InitializeViewModel()
    {
        try
        {
            ViewModel = App.Services.GetService<MainWindowViewModel>();

            if (ViewModel != null)
            {
                WindowsFilterButton.Command = ViewModel.ToggleWindowsFilterCommand;
                DonateButton.Command = ViewModel.DonateCommand;
                BugReportButton.Command = ViewModel.BugReportCommand;
                DocsButton.Command = ViewModel.DocsCommand;

                UpdateFilterButtonIcon();

                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                ViewModel.UpdateCheck.PropertyChanged += UpdateCheck_PropertyChanged;
                ViewModel.ReviewModeBar.PropertyChanged += ReviewModeBar_PropertyChanged;
                ViewModel.BuilderModeBar.PropertyChanged += BuilderModeBar_PropertyChanged;

                ViewModel.Initialize();

                UpdateAppIcon();

                var versionService = App.Services.GetService<IVersionService>();
                if (versionService?.GetCurrentVersion().IsBeta == true)
                {
                    BetaBannerText.Visibility = Visibility.Visible;
                }

                NavSidebar.ViewModel = ViewModel;

                _taskProgressCoordinator = new TaskProgressCoordinator(
                    TaskProgressControl, TaskProgressControl2, TaskProgressControl3,
                    _logService!, this.DispatcherQueue);

                TaskProgressControl.CancelCommand = ViewModel.TaskProgress.CancelCommand;
                TaskProgressControl.CancelText = ViewModel.TaskProgress.CancelButtonLabel;
                TaskProgressControl.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;
                TaskProgressControl2.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;
                TaskProgressControl3.ShowDetailsCommand = ViewModel.TaskProgress.ShowDetailsCommand;

                // Pending Explorer restart bar. The pending state changes on background apply threads,
                // so the ViewModel's notification is marshalled onto the dispatcher before Refresh()
                // touches any bound property.
                _pendingRestartViewModel = App.Services.GetService<PendingRestartViewModel>();
                if (_pendingRestartViewModel is { } pendingRestartVm)
                {
                    PendingRestartBar.ViewModel = pendingRestartVm;
                }

                ViewModel.TaskProgress.PropertyChanged += (s, e) =>
                    _taskProgressCoordinator?.HandlePropertyChanged(ViewModel.TaskProgress, e.PropertyName);
                ViewModel.TaskProgress.ScriptProgressReceived +=
                    (slotIndex, detail) => _taskProgressCoordinator?.HandleScriptProgressReceived(slotIndex, detail);

                _configReviewService = App.Services.GetService<IConfigReviewService>();
                _navBadgeService = App.Services.GetService<INavBadgeService>();
                _navigationRouter = new NavigationRouter(
                    _configReviewService, _navBadgeService, this.DispatcherQueue);

                if (_configReviewService != null)
                {
                    _configReviewService.ReviewModeChanged += OnReviewModeBadgeChanged;
                    _configReviewService.BadgeStateChanged += OnBadgeStateChanged;
                }

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

    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        NavSidebar.TogglePane();
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

    private void OnReviewModeBadgeChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_configReviewService?.IsInReviewMode == true)
            {
                _navBadgeService?.SubscribeToSoftwareAppsChanges(() =>
                    DispatcherQueue.TryEnqueue(ApplyNavBadges));
                ApplyNavBadges();

                var localizationService = App.Services.GetService<ILocalizationService>();
                NavSidebar.SetButtonLocked("AdvancedTools", true,
                    localizationService.GetStringOrDefault("Nav_AdvancedTools_Locked_Tooltip", "Unavailable during config review"));

                var currentTag = _navigationRouter?.GetTagForCurrentPage(ContentFrame.CurrentSourcePageType);
                if (currentTag == "AdvancedTools")
                {
                    _navigationRouter?.NavigateToPage(ContentFrame, "SoftwareApps", applyNavBadges: ApplyNavBadges);
                    NavSidebar.SelectedTag = "SoftwareApps";
                }
            }
            else
            {
                NavSidebar.ClearAllBadges();
                _navBadgeService?.UnsubscribeFromSoftwareAppsChanges();

                NavSidebar.SetButtonLocked("AdvancedTools", false);
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

    // Only what genuinely needs code-behind: BitmapImage creation, geometry conversion, opacity, and the Narrator
    // announcement; the rest is x:Bind.
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.AppIconSource))
        {
            DispatcherQueue.TryEnqueue(UpdateAppIcon);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowsFilterTooltip) && ViewModel != null)
        {
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
            DispatcherQueue.TryEnqueue(() =>
            {
                WindowsFilterIcon.Opacity = ViewModel.IsWindowsFilterButtonEnabled ? 1.0 : 0.4;
            });
        }
    }

    // Dynamic action-button creation cannot be done in XAML; the rest is x:Bind.
    private void UpdateCheck_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.PropertyName == nameof(UpdateCheckViewModel.IsUpdateActionButtonVisible)
            || e.PropertyName == nameof(UpdateCheckViewModel.InstallNowButtonText)
            || e.PropertyName == nameof(UpdateCheckViewModel.IsRelaunchButtonVisible)
            || e.PropertyName == nameof(UpdateCheckViewModel.RelaunchButtonText))
        {
            DispatcherQueue.TryEnqueue(UpdateInfoBarActionButton);
        }
    }

    // Narrator announcements and the ReviewModeBar visibility toggle; the rest is x:Bind.
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

    private void UpdateFilterButtonIcon()
    {
        try
        {
            if (ViewModel != null && WindowsFilterIcon != null)
            {
                WindowsFilterIcon.Data = GeometryHelper.FromPathData(ViewModel.WindowsFilterIcon);
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

        if (uc.IsRelaunchButtonVisible)
        {
            var button = new Button
            {
                Content = uc.RelaunchButtonText,
                Command = uc.RelaunchCommand,
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            };
            UpdateInfoBar.ActionButton = button;
        }
        else if (uc.IsUpdateActionButtonVisible)
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
        if (ViewModel == null) return;

        if (e.PropertyName == nameof(BuilderModeBarViewModel.IsBuilderActive))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                BuilderModeBarGrid.Visibility = ViewModel.BuilderModeBar.IsBuilderActive
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            });
        }
    }

    private void BuilderSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.BuilderModeBar.SaveCommand.CanExecute(null) == true)
            ViewModel.BuilderModeBar.SaveCommand.Execute(null);
    }

    private void BuilderCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.BuilderModeBar.CancelCommand.CanExecute(null) == true)
            ViewModel.BuilderModeBar.CancelCommand.Execute(null);
    }

    private void BuilderConfigRadio_Checked(object sender, RoutedEventArgs e)
    {
        ViewModel?.BuilderModeBar.SelectConfigTarget();
    }

    private void BuilderAutounattendRadio_Checked(object sender, RoutedEventArgs e)
    {
        ViewModel?.BuilderModeBar.SelectAutounattendTarget();
    }

    private void BuilderOtherHardwareCheck_Changed(object sender, RoutedEventArgs e)
    {
        ViewModel?.BuilderModeBar.SetShowOtherHardwareAsync(BuilderOtherHardwareCheck.IsChecked == true).FireAndForget(_logService!);
    }

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

    private void ZoomInAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _uiZoomManager?.StepUp();
        args.Handled = true;
    }

    private void ZoomOutAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _uiZoomManager?.StepDown();
        args.Handled = true;
    }

    private void ZoomResetAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _uiZoomManager?.Reset();
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

        e.Handled = true;
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
            VirtualKey.Number1 => WindowsFilterButton,
            VirtualKey.Number2 => DonateButton,
            VirtualKey.Number3 => BugReportButton,
            VirtualKey.Number4 => DocsButton,
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
}
