using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.EventHandlers;
using Winhance.UI.Features.AdvancedTools;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Customize;
using Winhance.UI.Features.Optimize;
using Winhance.UI.Features.Settings;
using Winhance.UI.Features.SoftwareApps;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Windows.Foundation;
using Windows.Graphics;

namespace Winhance.UI;

/// <summary>
/// Main application window with custom NavSidebar navigation.
/// </summary>
public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private WindowSizeManager? _windowSizeManager;
    private IConfigReviewService? _configReviewService;
    private ILogService? _logService;
    private BackupResult? _backupResult;
    private bool _isStartupLoading = true;
    private bool _softwareAppsBadgeSubscribed;

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

        // Apply theme-aware caption button colors and update when theme changes
        RootGrid.ActualThemeChanged += (_, _) => ApplySystemThemeToCaptionButtons();
        RootGrid.Loaded += (_, _) => ApplySystemThemeToCaptionButtons();

        // Initialize window size manager for position/size persistence
        InitializeWindowSizeManager();

        // Apply Mica backdrop (Windows 11) with fallback to DesktopAcrylic (Windows 10)
        TrySetMicaBackdrop();

        // Initialize DispatcherService - MUST be done before any service uses it
        // This is required because DispatcherQueue is only available after window creation
        InitializeDispatcherService();

        // Apply initial FlowDirection for RTL languages and subscribe to language changes
        InitializeFlowDirection();

        // Set up title bar after loaded
        AppTitleBar.Loaded += AppTitleBar_Loaded;

        // Set default navigation after sidebar is loaded
        NavSidebar.Loaded += NavSidebar_Loaded;
    }

    /// <summary>
    /// Sets the default navigation item after the NavSidebar is loaded.
    /// </summary>
    private void NavSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        StartupLogger.Log("MainWindow", "NavSidebar_Loaded");

        // Subscribe to MoreMenuClosed to restore selection based on current page
        NavSidebar.MoreMenuClosed += NavSidebar_MoreMenuClosed;

        // Skip auto-navigation during startup — CompleteStartup() will trigger it
        if (_isStartupLoading)
        {
            StartupLogger.Log("MainWindow", "Startup loading in progress, deferring navigation");
            return;
        }

        // Navigate to SoftwareApps page by default
        StartupLogger.Log("MainWindow", "Navigating to SoftwareApps as default...");
        NavSidebar.SelectedTag = "SoftwareApps";
        NavigateToPage("SoftwareApps");
        StartupLogger.Log("MainWindow", "SoftwareApps selected");
    }

    /// <summary>
    /// Handles the More menu closing by restoring selection to the current page.
    /// </summary>
    private void NavSidebar_MoreMenuClosed(object? sender, EventArgs e)
    {
        // Get the tag for the currently displayed page
        var currentTag = GetTagForCurrentPage();
        if (!string.IsNullOrEmpty(currentTag))
        {
            NavSidebar.SelectedTag = currentTag;
        }
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

            // Initialize async (restore saved position/size or set defaults)
            _ = _windowSizeManager.InitializeAsync();

            // Wire up ApplicationCloseService: saves window state, shows donation dialog, then exits
            var applicationCloseService = App.Services.GetRequiredService<IApplicationCloseService>();
            applicationCloseService.BeforeShutdown = async () =>
            {
                if (_windowSizeManager != null)
                {
                    await _windowSizeManager.SaveWindowSettingsAsync();
                }
            };

            // Intercept window close — cancel the native close and delegate to ApplicationCloseService
            this.AppWindow.Closing += async (sender, args) =>
            {
                args.Cancel = true;
                await applicationCloseService.CheckOperationsAndCloseAsync();
            };
        }
        catch (Exception ex)
        {
            // Fallback to a reasonable default if WindowSizeManager fails
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
            _logService?.LogDebug($"Failed to initialize WindowSizeManager: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the navigation tag for the currently displayed page.
    /// </summary>
    private string? GetTagForCurrentPage()
    {
        var pageType = ContentFrame.CurrentSourcePageType;
        if (pageType == null) return null;

        return pageType.Name switch
        {
            nameof(SettingsPage) => "Settings",
            nameof(OptimizePage) => "Optimize",
            nameof(CustomizePage) => "Customize",
            nameof(AdvancedToolsPage) => "AdvancedTools",
            nameof(SoftwareAppsPage) => "SoftwareApps",
            _ => null
        };
    }

    /// <summary>
    /// Attempts to set Mica backdrop for Windows 11, falls back to DesktopAcrylic for Windows 10.
    /// </summary>
    private void TrySetMicaBackdrop()
    {
        try
        {
            // Try Mica first (Windows 11)
            if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
                return;
            }

            // Fall back to DesktopAcrylic (Windows 10)
            if (DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
        }
        catch (Exception ex)
        {
            // Backdrop not supported or failed - continue without it
            _logService?.LogDebug($"Failed to set backdrop: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the initial FlowDirection based on the current language and subscribes
    /// to language changes so RTL languages (e.g. Arabic) mirror the entire UI.
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

    /// <summary>
    /// Applies the appropriate FlowDirection to the root grid and refreshes title bar padding.
    /// </summary>
    private void ApplyFlowDirection(bool isRightToLeft)
    {
        RootGrid.FlowDirection = isRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        // Re-apply title bar padding since column positions flip with FlowDirection
        if (AppTitleBar.IsLoaded)
        {
            SetTitleBarPadding();
        }
    }

    /// <summary>
    /// Called when the root grid is loaded. Sets up services that need XamlRoot.
    /// </summary>
    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize DialogService with XamlRoot - required for ContentDialog
        InitializeDialogService();
    }

    /// <summary>
    /// Initializes the dispatcher service with this window's DispatcherQueue.
    /// </summary>
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

    /// <summary>
    /// Initializes the dialog service with XamlRoot for ContentDialog support.
    /// </summary>
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

    /// <summary>
    /// Handles the pane toggle button click in the title bar.
    /// </summary>
    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        NavSidebar.TogglePane();
    }

    #region Review Mode Badges

    private void OnReviewModeBadgeChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_configReviewService?.IsInReviewMode == true)
            {
                SubscribeToSoftwareAppsChanges();
                UpdateNavBadges();
            }
            else
            {
                NavSidebar.ClearAllBadges();
                UnsubscribeFromSoftwareAppsChanges();
            }
        });
    }

    private void OnBadgeStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateNavBadges);
    }

    private void UpdateNavBadges()
    {
        if (_configReviewService == null || !_configReviewService.IsInReviewMode) return;

        foreach (var tag in new[] { "SoftwareApps", "Optimize", "Customize" })
        {
            // For SoftwareApps in review mode, use actual VM selections for reactive badge
            var count = tag == "SoftwareApps" && _softwareAppsBadgeSubscribed
                ? GetSoftwareAppsSelectedCount()
                : _configReviewService.GetNavBadgeCount(tag);

            if (count > 0)
            {
                if (_configReviewService.IsSectionFullyReviewed(tag))
                {
                    // Fully reviewed: show checkmark only (no count number)
                    NavSidebar.SetButtonBadge(tag, -1, "SuccessIcon");
                }
                else
                {
                    // Not fully reviewed: show attention badge with count
                    NavSidebar.SetButtonBadge(tag, count, "Attention");
                }
            }
            else
            {
                // Check if this section has any features in the config (0 diffs = all match)
                bool sectionInConfig = tag switch
                {
                    "SoftwareApps" => _configReviewService.IsFeatureInConfig(FeatureIds.WindowsApps)
                                   || _configReviewService.IsFeatureInConfig(FeatureIds.ExternalApps),
                    "Optimize" => FeatureDefinitions.OptimizeFeatures.Any(f => _configReviewService.IsFeatureInConfig(f)),
                    "Customize" => FeatureDefinitions.CustomizeFeatures.Any(f => _configReviewService.IsFeatureInConfig(f)),
                    _ => false
                };

                if (sectionInConfig)
                {
                    // Show success checkmark badge only (no count number)
                    NavSidebar.SetButtonBadge(tag, -1, "SuccessIcon");
                }
                else
                {
                    NavSidebar.SetButtonBadge(tag, -1, string.Empty);
                }
            }
        }
    }

    private void SubscribeToSoftwareAppsChanges()
    {
        if (_softwareAppsBadgeSubscribed) return;

        var windowsAppsVm = App.Services.GetService<WindowsAppsViewModel>();
        var externalAppsVm = App.Services.GetService<ExternalAppsViewModel>();

        if (windowsAppsVm != null)
            windowsAppsVm.PropertyChanged += OnSoftwareAppsPropertyChanged;
        if (externalAppsVm != null)
            externalAppsVm.PropertyChanged += OnSoftwareAppsPropertyChanged;

        _softwareAppsBadgeSubscribed = true;
    }

    private void UnsubscribeFromSoftwareAppsChanges()
    {
        if (!_softwareAppsBadgeSubscribed) return;

        var windowsAppsVm = App.Services.GetService<WindowsAppsViewModel>();
        var externalAppsVm = App.Services.GetService<ExternalAppsViewModel>();

        if (windowsAppsVm != null)
            windowsAppsVm.PropertyChanged -= OnSoftwareAppsPropertyChanged;
        if (externalAppsVm != null)
            externalAppsVm.PropertyChanged -= OnSoftwareAppsPropertyChanged;

        _softwareAppsBadgeSubscribed = false;
    }

    private void OnSoftwareAppsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == "HasSelectedItems" || e.PropertyName == "IsSelected") && _configReviewService?.IsInReviewMode == true)
        {
            DispatcherQueue.TryEnqueue(UpdateNavBadges);
        }
    }

    private int GetSoftwareAppsSelectedCount()
    {
        var windowsAppsVm = App.Services.GetService<WindowsAppsViewModel>();
        var externalAppsVm = App.Services.GetService<ExternalAppsViewModel>();

        int count = 0;
        if (windowsAppsVm?.Items != null)
            count += windowsAppsVm.Items.Count(a => a.IsSelected);
        if (externalAppsVm?.Items != null)
            count += externalAppsVm.Items.Count(a => a.IsSelected);
        return count;
    }

    #endregion

    /// <summary>
    /// Handles navigation when a NavButton is clicked in the sidebar.
    /// </summary>
    private void NavSidebar_ItemClicked(object sender, NavButtonClickedEventArgs e)
    {
        var tag = e.NavigationTag?.ToString();
        StartupLogger.Log("MainWindow", $"NavSidebar_ItemClicked - Tag: {tag}");

        // Special handling for More button - show flyout instead of navigating
        if (tag == "More")
        {
            ShowMoreMenuFlyout();
            return;
        }

        NavigateToPage(tag);
    }

    /// <summary>
    /// Shows the More menu flyout - delegates to NavSidebar which owns the flyout.
    /// </summary>
    private void ShowMoreMenuFlyout()
    {
        NavSidebar.ShowMoreMenuFlyout();
    }

    /// <summary>
    /// Navigates to the specified page based on the tag.
    /// </summary>
    private void NavigateToPage(string? tag)
    {
        StartupLogger.Log("MainWindow", $"NavigateToPage called with tag: {tag}");
        Type? pageType = tag switch
        {
            "Settings" => typeof(SettingsPage),
            "Optimize" => typeof(OptimizePage),
            "Customize" => typeof(CustomizePage),
            "AdvancedTools" => typeof(AdvancedToolsPage),
            "SoftwareApps" => typeof(SoftwareAppsPage),
            "More" => null, // More button could open a flyout or dialog
            _ => null
        };

        StartupLogger.Log("MainWindow", $"Resolved page type: {pageType?.Name ?? "null"}");

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            try
            {
                StartupLogger.Log("MainWindow", $"Navigating to {pageType.Name}...");
                var result = ContentFrame.Navigate(pageType);
                StartupLogger.Log("MainWindow", $"Navigate result: {result}");

                // Mark SoftwareApps features as visited when navigating to that page
                if (tag == "SoftwareApps" && _configReviewService?.IsInReviewMode == true)
                {
                    _configReviewService.MarkFeatureVisited(FeatureIds.WindowsApps);
                    _configReviewService.MarkFeatureVisited(FeatureIds.ExternalApps);
                    SubscribeToSoftwareAppsChanges();
                }
            }
            catch (Exception ex)
            {
                StartupLogger.Log("MainWindow", $"Navigation EXCEPTION: {ex}");
            }
        }
        else
        {
            StartupLogger.Log("MainWindow", $"Skipping navigation - pageType null or same page");
        }
    }

    /// <summary>
    /// Kicks off the async startup sequence. Called by App.xaml.cs after Activate + InitializeTheme.
    /// </summary>
    public void StartStartupOperations()
    {
        StartupLogger.Log("MainWindow", "StartStartupOperations called");
        UpdateLoadingLogo();

        // Set all overlay text from localization keys
        try
        {
            var localizationService = App.Services.GetService<ILocalizationService>();
            if (localizationService != null)
            {
                LoadingTitleText.Text = localizationService.GetString("App_Title");
                LoadingTaglineText.Text = localizationService.GetString("App_Tagline");
                LoadingStatusText.Text = localizationService.GetString("Loading_PreparingApp");
            }
        }
        catch { }

        _ = RunStartupSequenceAsync();
    }

    /// <summary>
    /// Orchestrates all startup operations asynchronously while updating the loading overlay.
    /// </summary>
    private async Task RunStartupSequenceAsync()
    {
        try
        {
            // 1. Initialize settings registry (was blocking in App.xaml.cs before)
            UpdateLoadingStatus("Loading_InitializingSettings");
            StartupLogger.Log("MainWindow", "Startup: Initializing settings registry...");
            try
            {
                var settingsRegistry = App.Services.GetRequiredService<ICompatibleSettingsRegistry>();
                await settingsRegistry.InitializeAsync().ConfigureAwait(false);

                var settingsPreloader = App.Services.GetRequiredService<IGlobalSettingsPreloader>();
                await settingsPreloader.PreloadAllSettingsAsync().ConfigureAwait(false);
                StartupLogger.Log("MainWindow", "Startup: Settings registry initialized");

                // Initialize tooltip event handler (constructor subscribes to EventBus)
                App.Services.GetRequiredService<TooltipRefreshEventHandler>();

                // Pre-cache regedit icon for Technical Details panel
                _ = RegeditIconProvider.GetIconAsync();
            }
            catch (Exception ex)
            {
                StartupLogger.Log("MainWindow", $"Startup: Settings registry FAILED: {ex.Message}");
                _logService?.LogWarning($"Failed to initialize settings registry: {ex.Message}");
            }

            // 2. User backup config (first-run only)
            try
            {
                var preferencesService = App.Services.GetRequiredService<IUserPreferencesService>();
                var backupCompleted = preferencesService.GetPreference(
                    UserPreferenceKeys.InitialConfigBackupCompleted, "false");
                if (!string.Equals(backupCompleted, "true", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateLoadingStatus("Loading_CreatingConfigBackup");
                    StartupLogger.Log("MainWindow", "Startup: Creating user backup config...");
                    var configService = App.Services.GetRequiredService<IConfigurationService>();

                    var backupTask = configService.CreateUserBackupConfigAsync();
                    var completed = await Task.WhenAny(
                        backupTask, Task.Delay(TimeSpan.FromSeconds(90))).ConfigureAwait(false);

                    if (completed == backupTask)
                    {
                        await backupTask; // observe exceptions
                        await preferencesService.SetPreferenceAsync(
                            UserPreferenceKeys.InitialConfigBackupCompleted, "true");
                        StartupLogger.Log("MainWindow", "Startup: User backup config done");
                    }
                    else
                    {
                        StartupLogger.Log("MainWindow",
                            "Startup: User backup config TIMED OUT (will retry next launch)");
                        _logService?.LogWarning(
                            "User backup config timed out after 90s — will retry next launch");
                    }
                }
                else
                {
                    StartupLogger.Log("MainWindow", "Startup: User backup config already completed");
                }
            }
            catch (Exception ex)
            {
                StartupLogger.Log("MainWindow", $"Startup: User backup config FAILED: {ex.Message}");
                _logService?.LogWarning($"User backup config failed: {ex.Message}");
            }

            // 3. System restore point (respects SkipSystemBackup preference)
            try
            {
                var preferencesService = App.Services.GetRequiredService<IUserPreferencesService>();
                var skipBackup = preferencesService.GetPreference(UserPreferenceKeys.SkipSystemBackup, "false");
                if (!string.Equals(skipBackup, "true", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateLoadingStatus("Loading_CheckingSystemProtection");
                    StartupLogger.Log("MainWindow", "Startup: Checking system protection...");
                    var backupService = App.Services.GetRequiredService<ISystemBackupService>();
                    var backupProgress = new Progress<Core.Features.Common.Models.TaskProgressDetail>(detail =>
                    {
                        if (!string.IsNullOrEmpty(detail.StatusText))
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                try { LoadingStatusText.Text = detail.StatusText; } catch { }
                            });
                        }
                    });
                    _backupResult = await backupService.EnsureInitialBackupsAsync(backupProgress).ConfigureAwait(false);
                    StartupLogger.Log("MainWindow", "Startup: System protection check done");
                }
                else
                {
                    StartupLogger.Log("MainWindow", "Startup: System backup skipped (user preference)");
                }
            }
            catch (Exception ex)
            {
                StartupLogger.Log("MainWindow", $"Startup: System backup FAILED: {ex.Message}");
                _logService?.LogWarning($"System backup failed: {ex.Message}");
            }

            // 4. Script migration
            try
            {
                UpdateLoadingStatus("Loading_MigratingScripts");
                StartupLogger.Log("MainWindow", "Startup: Migrating scripts...");
                var migrationService = App.Services.GetRequiredService<IScriptMigrationService>();
                await migrationService.MigrateFromOldPathsAsync().ConfigureAwait(false);
                StartupLogger.Log("MainWindow", "Startup: Script migration done");
            }
            catch (Exception ex)
            {
                StartupLogger.Log("MainWindow", $"Startup: Script migration FAILED: {ex.Message}");
                _logService?.LogWarning($"Script migration failed: {ex.Message}");
            }

            // 5. Script updates
            try
            {
                UpdateLoadingStatus("Loading_CheckingScripts");
                StartupLogger.Log("MainWindow", "Startup: Checking for script updates...");
                var updateService = App.Services.GetRequiredService<IRemovalScriptUpdateService>();
                await updateService.CheckAndUpdateScriptsAsync().ConfigureAwait(false);
                StartupLogger.Log("MainWindow", "Startup: Script update check done");
            }
            catch (Exception ex)
            {
                StartupLogger.Log("MainWindow", $"Startup: Script update check FAILED: {ex.Message}");
                _logService?.LogWarning($"Script update check failed: {ex.Message}");
            }

            // 6. Complete startup — navigate to SoftwareApps and wait for it to finish loading
            UpdateLoadingStatus("Loading_PreparingApp");
            StartupLogger.Log("MainWindow", "Startup: Completing startup...");
            DispatcherQueue.TryEnqueue(() => _ = CompleteStartupAsync());
        }
        catch (Exception ex)
        {
            StartupLogger.Log("MainWindow", $"Startup: RunStartupSequenceAsync EXCEPTION: {ex}");
            // Even on failure, try to complete startup so the app is usable
            DispatcherQueue.TryEnqueue(() => _ = CompleteStartupAsync());
        }
    }

    /// <summary>
    /// Updates the loading status text on the UI thread.
    /// </summary>
    private void UpdateLoadingStatus(string localizationKey)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var localizationService = App.Services.GetService<ILocalizationService>();
                LoadingStatusText.Text = localizationService?.GetString(localizationKey) ?? localizationKey;
            }
            catch
            {
                LoadingStatusText.Text = localizationKey;
            }
        });
    }

    /// <summary>
    /// Navigates to SoftwareApps, waits for its installation status checks to finish,
    /// then hides the loading overlay so the UI is fully ready.
    /// </summary>
    private async Task CompleteStartupAsync()
    {
        StartupLogger.Log("MainWindow", "CompleteStartupAsync starting");

        try
        {
            // Navigate to SoftwareApps with "startup" parameter to prevent double-init
            NavSidebar.SelectedTag = "SoftwareApps";
            ContentFrame.Navigate(typeof(SoftwareAppsPage), "startup");

            // Wait for the SoftwareApps page to finish loading apps + installation status
            var page = ContentFrame.Content as SoftwareAppsPage;
            if (page != null)
            {
                StartupLogger.Log("MainWindow", "Awaiting SoftwareApps initialization...");
                await page.ViewModel.InitializeAsync();
                StartupLogger.Log("MainWindow", "SoftwareApps initialization complete");
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log("MainWindow", $"SoftwareApps initialization failed: {ex.Message}");
            _logService?.LogWarning($"SoftwareApps init failed: {ex.Message}");
        }

        // Hide overlay and mark startup complete
        _isStartupLoading = false;
        LoadingOverlay.Visibility = Visibility.Collapsed;
        StartupLogger.Log("MainWindow", "Startup complete, overlay hidden");

        // Show backup notification dialog if backups were created
        try
        {
            var startupNotifications = App.Services.GetRequiredService<IStartupNotificationService>();
            await startupNotifications.ShowBackupNotificationAsync(_backupResult);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("MainWindow", $"Startup notification failed: {ex.Message}");
        }

        // Check for updates silently (only shows InfoBar if update available)
        // Ensure WinGet is ready (shows task progress if installation/update needed)
        if (_viewModel != null)
        {
            _ = _viewModel.CheckForUpdatesOnStartupAsync();
            _ = _viewModel.EnsureWinGetReadyOnStartupAsync();
        }
    }

    /// <summary>
    /// Sets the loading overlay logo based on the current theme.
    /// </summary>
    private void UpdateLoadingLogo()
    {
        try
        {
            var isDark = RootGrid.ActualTheme != ElementTheme.Light;
            var logoUri = isDark
                ? "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"
                : "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png";
            LoadingLogo.Source = new BitmapImage(new Uri(logoUri));
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to set loading logo: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows the loading overlay.
    /// </summary>
    public void ShowLoadingOverlay()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the loading overlay.
    /// </summary>
    public void HideLoadingOverlay()
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Sets the loading state for a specific navigation button.
    /// </summary>
    /// <param name="tag">The navigation tag of the button.</param>
    /// <param name="isLoading">Whether the button should show loading state.</param>
    public void SetNavButtonLoading(string tag, bool isLoading)
    {
        NavSidebar.SetButtonLoading(tag, isLoading);
    }

    #region Keyboard Accelerators

    /// <summary>
    /// Handles Ctrl+1 through Ctrl+5 keyboard shortcuts to navigate between sections.
    /// </summary>
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
            NavigateToPage(tag);

            // Focus the NavButton so Narrator announces the page name
            var navButton = NavSidebar.GetButton(tag);
            navButton?.Focus(FocusState.Keyboard);

            args.Handled = true;
        }
    }

    /// <summary>
    /// Handles Ctrl+S keyboard shortcut to save configuration.
    /// </summary>
    private void SaveConfigAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_viewModel?.SaveConfigCommand.CanExecute(null) == true)
        {
            _viewModel.SaveConfigCommand.Execute(null);
        }
        args.Handled = true;
    }

    /// <summary>
    /// Handles Ctrl+6 keyboard shortcut to open the More menu.
    /// </summary>
    private void MoreMenuAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ShowMoreMenuFlyout();
        args.Handled = true;
    }

    /// <summary>
    /// Handles Ctrl+Shift+1 through Ctrl+Shift+6 keyboard shortcuts for title bar action buttons.
    /// Announces the button name to Narrator after execution.
    /// </summary>
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

        // Announce after a short delay so async state changes (e.g. filter toggle) are reflected
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

    #region Title Bar

    /// <summary>
    /// Called when the title bar is loaded. Sets up ViewModel bindings and padding.
    /// </summary>
    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        // Set up caption button padding
        SetTitleBarPadding();

        // Defer passthrough region setup to ensure all elements are laid out
        DispatcherQueue.TryEnqueue(() => SetTitleBarPassthroughRegions());

        // Update passthrough regions when title bar size changes
        AppTitleBar.SizeChanged += (_, _) => SetTitleBarPassthroughRegions();
        TitleBarButtons.SizeChanged += (_, _) => SetTitleBarPassthroughRegions();

        // Initialize ViewModel and wire up bindings
        InitializeViewModel();
    }

    /// <summary>
    /// Sets up passthrough regions for interactive elements in the title bar.
    /// This prevents double-clicks on buttons from maximizing the window.
    /// </summary>
    private void SetTitleBarPassthroughRegions()
    {
        try
        {
            var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);
            var scale = AppTitleBar.XamlRoot?.RasterizationScale ?? 1.0;

            var passthroughRects = new List<RectInt32>();

            // Add passthrough region for the pane toggle button
            AddElementPassthroughRect(PaneToggleButton, scale, passthroughRects);

            // Add passthrough region for the entire title bar buttons container
            // This ensures all buttons are covered regardless of individual positioning
            AddElementPassthroughRect(TitleBarButtons, scale, passthroughRects);

            if (passthroughRects.Count > 0)
            {
                nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, passthroughRects.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to set title bar passthrough regions: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds an element's bounds to the passthrough rectangles list.
    /// </summary>
    private void AddElementPassthroughRect(FrameworkElement element, double scale, List<RectInt32> rects)
    {
        try
        {
            var transform = element.TransformToVisual(null);
            var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

            rects.Add(new RectInt32(
                _X: (int)Math.Round(bounds.X * scale),
                _Y: (int)Math.Round(bounds.Y * scale),
                _Width: (int)Math.Round(bounds.Width * scale),
                _Height: (int)Math.Round(bounds.Height * scale)
            ));
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to add passthrough rect for {element.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the ViewModel and wires up button commands.
    /// </summary>
    private void InitializeViewModel()
    {
        try
        {
            _viewModel = App.Services.GetService<MainWindowViewModel>();

            if (_viewModel != null)
            {
                // Wire up button commands
                SaveConfigButton.Command = _viewModel.SaveConfigCommand;
                ImportConfigButton.Command = _viewModel.ImportConfigCommand;
                WindowsFilterButton.Command = _viewModel.ToggleWindowsFilterCommand;
                DonateButton.Command = _viewModel.DonateCommand;
                BugReportButton.Command = _viewModel.BugReportCommand;
                DocsButton.Command = _viewModel.DocsCommand;

                // Set initial filter button icon
                UpdateFilterButtonIcon();

                // Wire up tooltips and accessible names
                ToolTipService.SetToolTip(PaneToggleButton, _viewModel.ToggleNavigationTooltip);
                AutomationProperties.SetName(PaneToggleButton, _viewModel.ToggleNavigationTooltip);
                ToolTipService.SetToolTip(SaveConfigButton, _viewModel.SaveConfigTooltip);
                AutomationProperties.SetName(SaveConfigButton, _viewModel.SaveConfigTooltip);
                ToolTipService.SetToolTip(ImportConfigButton, _viewModel.ImportConfigTooltip);
                AutomationProperties.SetName(ImportConfigButton, _viewModel.ImportConfigTooltip);
                ToolTipService.SetToolTip(WindowsFilterButton, _viewModel.WindowsFilterTooltip);
                AutomationProperties.SetName(WindowsFilterButton, _viewModel.WindowsFilterTooltip);
                ToolTipService.SetToolTip(DonateButton, _viewModel.DonateTooltip);
                AutomationProperties.SetName(DonateButton, _viewModel.DonateTooltip);
                ToolTipService.SetToolTip(BugReportButton, _viewModel.BugReportTooltip);
                AutomationProperties.SetName(BugReportButton, _viewModel.BugReportTooltip);
                ToolTipService.SetToolTip(DocsButton, _viewModel.DocsTooltip);
                AutomationProperties.SetName(DocsButton, _viewModel.DocsTooltip);

                // Subscribe to icon changes
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;

                // Set initial icon
                UpdateAppIcon();

                // Set localized app title and subtitle
                AppTitleTextBlock.Text = _viewModel.AppTitle;
                AppSubtitleTextBlock.Text = _viewModel.AppSubtitle;

                // Show beta banner if this is a beta build
                var versionService = App.Services.GetService<IVersionService>();
                if (versionService?.GetCurrentVersion().IsBeta == true)
                {
                    BetaBannerText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                }

                // Pass ViewModel to NavSidebar for localized nav button text
                NavSidebar.ViewModel = _viewModel;

                // Wire up Task Progress Control
                TaskProgressControl.CancelCommand = _viewModel.CancelCommand;
                TaskProgressControl.CancelText = _viewModel.CancelButtonLabel;

                // Subscribe to multi-script progress updates
                _viewModel.ScriptProgressReceived += OnScriptProgressReceived;

                // Load filter preference asynchronously
                _ = _viewModel.LoadFilterPreferenceAsync();

                // Subscribe to review mode badge events
                _configReviewService = App.Services.GetService<IConfigReviewService>();
                if (_configReviewService != null)
                {
                    _configReviewService.ReviewModeChanged += OnReviewModeBadgeChanged;
                    _configReviewService.BadgeStateChanged += OnBadgeStateChanged;
                }
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to initialize ViewModel: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles ViewModel property changes.
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.AppIconSource))
        {
            DispatcherQueue.TryEnqueue(UpdateAppIcon);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.AppTitle) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => AppTitleTextBlock.Text = _viewModel.AppTitle);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.AppSubtitle) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => AppSubtitleTextBlock.Text = _viewModel.AppSubtitle);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ToggleNavigationTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ToolTipService.SetToolTip(PaneToggleButton, _viewModel.ToggleNavigationTooltip);
                AutomationProperties.SetName(PaneToggleButton, _viewModel.ToggleNavigationTooltip);
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.SaveConfigTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ToolTipService.SetToolTip(SaveConfigButton, _viewModel.SaveConfigTooltip);
                AutomationProperties.SetName(SaveConfigButton, _viewModel.SaveConfigTooltip);
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ImportConfigTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ToolTipService.SetToolTip(ImportConfigButton, _viewModel.ImportConfigTooltip);
                AutomationProperties.SetName(ImportConfigButton, _viewModel.ImportConfigTooltip);
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowsFilterTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var tooltip = _viewModel.WindowsFilterTooltip;
                ToolTipService.SetToolTip(WindowsFilterButton, tooltip);
                AutomationProperties.SetName(WindowsFilterButton, tooltip);

                // Announce the new filter state so Narrator reports ON/OFF after toggle
                var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(WindowsFilterButton)
                           ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(WindowsFilterButton);
                peer?.RaiseNotificationEvent(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
                    Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.ImportantMostRecent,
                    tooltip,
                    "FilterStateChanged");
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.DonateTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ToolTipService.SetToolTip(DonateButton, _viewModel.DonateTooltip);
                AutomationProperties.SetName(DonateButton, _viewModel.DonateTooltip);
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.BugReportTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ToolTipService.SetToolTip(BugReportButton, _viewModel.BugReportTooltip);
                AutomationProperties.SetName(BugReportButton, _viewModel.BugReportTooltip);
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.DocsTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ToolTipService.SetToolTip(DocsButton, _viewModel.DocsTooltip);
                AutomationProperties.SetName(DocsButton, _viewModel.DocsTooltip);
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowsFilterIcon) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(UpdateFilterButtonIcon);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsLoading) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Skip single-task IsLoading updates when multi-script mode is active
                if (_viewModel.ActiveScriptCount > 0) return;

                TaskProgressControl.IsProgressVisible = _viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                TaskProgressControl.CanCancel = _viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                TaskProgressControl.IsTaskRunning = _viewModel.IsLoading;
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.AppName) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => TaskProgressControl.AppName = _viewModel.AppName);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.LastTerminalLine) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => TaskProgressControl.LastTerminalLine = _viewModel.LastTerminalLine);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.CancelButtonLabel) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => TaskProgressControl.CancelText = _viewModel.CancelButtonLabel);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.QueueStatusText) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => TaskProgressControl.QueueStatusText = _viewModel.QueueStatusText);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.QueueNextItemName) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => TaskProgressControl.QueueNextItemName = _viewModel.QueueNextItemName);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsQueueVisible) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
                TaskProgressControl.IsQueueInfoVisible = _viewModel.IsQueueVisible ? Visibility.Visible : Visibility.Collapsed);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ActiveScriptCount) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => UpdateMultiScriptControls(_viewModel.ActiveScriptCount));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsUpdateInfoBarOpen) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => UpdateInfoBar.IsOpen = _viewModel.IsUpdateInfoBarOpen);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.UpdateInfoBarTitle) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => UpdateInfoBar.Title = _viewModel.UpdateInfoBarTitle);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.UpdateInfoBarMessage) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => UpdateInfoBar.Message = _viewModel.UpdateInfoBarMessage);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.UpdateInfoBarSeverity) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => UpdateInfoBar.Severity = _viewModel.UpdateInfoBarSeverity);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsUpdateActionButtonVisible) && _viewModel != null
              || e.PropertyName == nameof(MainWindowViewModel.InstallNowButtonText) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(UpdateInfoBarActionButton);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsWindowsFilterButtonEnabled) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                WindowsFilterButton.IsEnabled = _viewModel.IsWindowsFilterButtonEnabled;
                WindowsFilterIcon.Opacity = _viewModel.IsWindowsFilterButtonEnabled ? 1.0 : 0.4;
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.IsInReviewMode) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ReviewModeBar.Visibility = _viewModel.IsInReviewMode ? Visibility.Visible : Visibility.Collapsed;
                if (_viewModel.IsInReviewMode)
                {
                    ReviewModeTitleText.Text = _viewModel.ReviewModeTitleText;
                    ReviewModeDescriptionText.Text = _viewModel.ReviewModeDescriptionText;
                    ReviewModeApplyButtonText.Text = _viewModel.ReviewModeApplyButtonText;
                    ReviewModeCancelButtonText.Text = _viewModel.ReviewModeCancelButtonText;
                    ReviewModeApplyButton.IsEnabled = _viewModel.CanApplyReviewedConfig;

                    // Update accessible names on review mode buttons from localized text
                    AutomationProperties.SetName(ReviewModeApplyButton, _viewModel.ReviewModeApplyButtonText);
                    AutomationProperties.SetName(ReviewModeCancelButton, _viewModel.ReviewModeCancelButtonText);

                    // Announce review mode entry to Narrator
                    var announcement = $"{_viewModel.ReviewModeTitleText}. {_viewModel.ReviewModeDescriptionText}";
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
        else if ((e.PropertyName == nameof(MainWindowViewModel.ReviewModeTitleText)
              || e.PropertyName == nameof(MainWindowViewModel.ReviewModeDescriptionText)
              || e.PropertyName == nameof(MainWindowViewModel.ReviewModeApplyButtonText)
              || e.PropertyName == nameof(MainWindowViewModel.ReviewModeCancelButtonText))
             && _viewModel?.IsInReviewMode == true)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ReviewModeTitleText.Text = _viewModel.ReviewModeTitleText;
                ReviewModeDescriptionText.Text = _viewModel.ReviewModeDescriptionText;
                ReviewModeApplyButtonText.Text = _viewModel.ReviewModeApplyButtonText;
                ReviewModeCancelButtonText.Text = _viewModel.ReviewModeCancelButtonText;
            });
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ReviewModeStatusText) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => ReviewModeStatusText.Text = _viewModel.ReviewModeStatusText);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.CanApplyReviewedConfig) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => ReviewModeApplyButton.IsEnabled = _viewModel.CanApplyReviewedConfig);
        }
    }

    /// <summary>
    /// Routes multi-script progress updates to the correct TaskProgressControl.
    /// Hides the control when its slot completes (Progress == 100).
    /// </summary>
    private void OnScriptProgressReceived(int slotIndex, TaskProgressDetail detail)
    {
        var control = slotIndex switch
        {
            0 => TaskProgressControl,
            1 => TaskProgressControl2,
            2 => TaskProgressControl3,
            _ => null
        };
        if (control == null) return;

        // Slot completed — hide this control only on intentional completion signal
        if (detail.IsCompletion)
        {
            control.IsProgressVisible = Visibility.Collapsed;
            control.IsTaskRunning = false;
            return;
        }

        if (!string.IsNullOrEmpty(detail.StatusText))
            control.AppName = detail.StatusText;
        control.LastTerminalLine = detail.TerminalOutput ?? "";
        if (detail.QueueTotal > 1)
        {
            control.IsQueueInfoVisible = Visibility.Visible;
            control.QueueStatusText = $"{detail.QueueCurrent} / {detail.QueueTotal}";
            control.QueueNextItemName = !string.IsNullOrEmpty(detail.QueueNextItemName)
                ? $"Next: {detail.QueueNextItemName}" : "";
        }
    }

    /// <summary>
    /// Shows/hides multi-script progress controls based on active slot count.
    /// No cancel buttons in multi-script mode.
    /// </summary>
    private void UpdateMultiScriptControls(int activeCount)
    {
        // Control 1
        TaskProgressControl.IsProgressVisible = activeCount >= 1 ? Visibility.Visible : Visibility.Collapsed;
        TaskProgressControl.IsTaskRunning = activeCount >= 1;
        TaskProgressControl.CanCancel = Visibility.Collapsed;

        // Control 2
        TaskProgressControl2.IsProgressVisible = activeCount >= 2 ? Visibility.Visible : Visibility.Collapsed;
        TaskProgressControl2.IsTaskRunning = activeCount >= 2;
        TaskProgressControl2.CanCancel = Visibility.Collapsed;

        // Control 3
        TaskProgressControl3.IsProgressVisible = activeCount >= 3 ? Visibility.Visible : Visibility.Collapsed;
        TaskProgressControl3.IsTaskRunning = activeCount >= 3;
        TaskProgressControl3.CanCancel = Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the Windows filter button icon based on the filter state.
    /// </summary>
    private void UpdateFilterButtonIcon()
    {
        try
        {
            if (_viewModel != null && WindowsFilterIcon != null)
            {
                var geometry = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry),
                    _viewModel.WindowsFilterIcon);
                WindowsFilterIcon.Data = geometry;
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to update filter button icon: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the InfoBar action button based on visibility state.
    /// </summary>
    private void UpdateInfoBarActionButton()
    {
        if (_viewModel == null) return;

        if (_viewModel.IsUpdateActionButtonVisible)
        {
            var button = new Button
            {
                Content = _viewModel.InstallNowButtonText,
                Command = _viewModel.InstallUpdateCommand,
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            };
            UpdateInfoBar.ActionButton = button;
        }
        else
        {
            UpdateInfoBar.ActionButton = null;
        }
    }

    /// <summary>
    /// Handles the InfoBar Closed event to sync ViewModel state.
    /// </summary>
    private void UpdateInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        _viewModel?.DismissUpdateInfoBar();
    }

    /// <summary>
    /// Handles the Review Mode Apply button click.
    /// </summary>
    private void ReviewModeApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.ApplyReviewedConfigCommand.CanExecute(null) == true)
            _viewModel.ApplyReviewedConfigCommand.Execute(null);
    }

    /// <summary>
    /// Handles the Review Mode Cancel button click.
    /// </summary>
    private void ReviewModeCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.CancelReviewModeCommand.CanExecute(null) == true)
            _viewModel.CancelReviewModeCommand.Execute(null);
    }

    /// <summary>
    /// Updates the app icon from the ViewModel.
    /// </summary>
    private void UpdateAppIcon()
    {
        try
        {
            if (_viewModel != null)
            {
                AppIcon.Source = new BitmapImage(new Uri(_viewModel.AppIconSource));
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to update app icon: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the padding columns to account for system caption buttons.
    /// </summary>
    private void SetTitleBarPadding()
    {
        try
        {
            var titleBar = this.AppWindow.TitleBar;
            var scale = AppTitleBar.XamlRoot?.RasterizationScale ?? 1.0;

            // When FlowDirection is RTL, the grid columns are visually mirrored:
            // LeftPaddingColumn (Column 0) renders on the physical right,
            // RightPaddingColumn (last column) renders on the physical left.
            // The system caption buttons remain physically on the right regardless of FlowDirection,
            // so we swap the inset assignments to keep padding aligned with the caption buttons.
            if (RootGrid.FlowDirection == FlowDirection.RightToLeft)
            {
                LeftPaddingColumn.Width = new GridLength(titleBar.RightInset / scale);
                RightPaddingColumn.Width = new GridLength(titleBar.LeftInset / scale);
            }
            else
            {
                RightPaddingColumn.Width = new GridLength(titleBar.RightInset / scale);
                LeftPaddingColumn.Width = new GridLength(titleBar.LeftInset / scale);
            }
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to set title bar padding: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies theme-aware colors to the caption buttons (minimize, maximize, close).
    /// This is a workaround as AppWindow TitleBar doesn't update caption button colors correctly when theme changes.
    /// Based on WinUI Gallery implementation.
    /// </summary>
    private void ApplySystemThemeToCaptionButtons()
    {
        try
        {
            var titleBar = this.AppWindow.TitleBar;
            var currentTheme = RootGrid.ActualTheme;

            // Set foreground colors based on theme
            var foregroundColor = currentTheme == ElementTheme.Dark
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black;

            titleBar.ButtonForegroundColor = foregroundColor;
            titleBar.ButtonHoverForegroundColor = foregroundColor;

            // Set hover background to subtle theme-aware color (~9% opacity)
            var hoverBackgroundColor = currentTheme == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(24, 255, 255, 255)  // Subtle white
                : Windows.UI.Color.FromArgb(24, 0, 0, 0);        // Subtle black

            titleBar.ButtonHoverBackgroundColor = hoverBackgroundColor;

            // Set other backgrounds to transparent
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to apply caption button colors: {ex.Message}");
        }
    }

    #endregion
}
