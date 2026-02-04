using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Customize;
using Winhance.UI.Features.Optimize;
using Winhance.UI.Features.Settings;
using Winhance.UI.Features.SoftwareApps;
using Winhance.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Foundation;
using Windows.Graphics;

namespace Winhance.UI;

/// <summary>
/// Main application window with custom NavSidebar navigation.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "startup-debug.log");
    private MainWindowViewModel? _viewModel;

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MainWindow] {message}{Environment.NewLine}");
        }
        catch { }
    }

    public MainWindow()
    {
        Log("Constructor starting...");
        this.InitializeComponent();
        Log("InitializeComponent completed");

        // Extend content into title bar for custom title bar experience
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Set tall title bar mode so caption buttons fill the full height
        this.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;

        // Apply theme-aware caption button colors and update when theme changes
        RootGrid.ActualThemeChanged += (_, _) => ApplySystemThemeToCaptionButtons();
        RootGrid.Loaded += (_, _) => ApplySystemThemeToCaptionButtons();

        // Set initial window size
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));

        // Apply Mica backdrop (Windows 11) with fallback to DesktopAcrylic (Windows 10)
        TrySetMicaBackdrop();

        // Initialize DispatcherService - MUST be done before any service uses it
        // This is required because DispatcherQueue is only available after window creation
        InitializeDispatcherService();

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
        Log("NavSidebar_Loaded");

        // Subscribe to MoreMenuClosed to restore selection based on current page
        NavSidebar.MoreMenuClosed += NavSidebar_MoreMenuClosed;

        // Navigate to Settings page by default (for testing - has fewer dependencies)
        Log("Navigating to Settings as default...");
        NavSidebar.SelectedTag = "Settings";
        NavigateToPage("Settings");
        Log("Settings selected");
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
            System.Diagnostics.Debug.WriteLine($"Failed to set backdrop: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to initialize DispatcherService: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to initialize DialogService: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the pane toggle button click in the title bar.
    /// </summary>
    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        NavSidebar.TogglePane();
    }

    /// <summary>
    /// Handles navigation when a NavButton is clicked in the sidebar.
    /// </summary>
    private void NavSidebar_ItemClicked(object sender, NavButtonClickedEventArgs e)
    {
        var tag = e.NavigationTag?.ToString();
        Log($"NavSidebar_ItemClicked - Tag: {tag}");

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
        Log($"NavigateToPage called with tag: {tag}");
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

        Log($"Resolved page type: {pageType?.Name ?? "null"}");

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            try
            {
                Log($"Navigating to {pageType.Name}...");
                var result = ContentFrame.Navigate(pageType);
                Log($"Navigate result: {result}");
            }
            catch (Exception ex)
            {
                Log($"Navigation EXCEPTION: {ex}");
            }
        }
        else
        {
            Log($"Skipping navigation - pageType null or same page");
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
            System.Diagnostics.Debug.WriteLine($"Failed to set title bar passthrough regions: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to add passthrough rect for {element.Name}: {ex.Message}");
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

                // Set initial filter button icon
                UpdateFilterButtonIcon();

                // Wire up tooltips
                ToolTipService.SetToolTip(SaveConfigButton, _viewModel.SaveConfigTooltip);
                ToolTipService.SetToolTip(ImportConfigButton, _viewModel.ImportConfigTooltip);
                ToolTipService.SetToolTip(WindowsFilterButton, _viewModel.WindowsFilterTooltip);
                ToolTipService.SetToolTip(DonateButton, _viewModel.DonateTooltip);
                ToolTipService.SetToolTip(BugReportButton, _viewModel.BugReportTooltip);

                // Subscribe to icon changes
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;

                // Set initial icon
                UpdateAppIcon();

                // Set localized app title and subtitle
                AppTitleTextBlock.Text = _viewModel.AppTitle;
                AppSubtitleTextBlock.Text = _viewModel.AppSubtitle;

                // Pass ViewModel to NavSidebar for localized nav button text
                NavSidebar.ViewModel = _viewModel;

                // Load filter preference asynchronously
                _ = _viewModel.LoadFilterPreferenceAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize ViewModel: {ex.Message}");
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
        else if (e.PropertyName == nameof(MainWindowViewModel.SaveConfigTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => ToolTipService.SetToolTip(SaveConfigButton, _viewModel.SaveConfigTooltip));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ImportConfigTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => ToolTipService.SetToolTip(ImportConfigButton, _viewModel.ImportConfigTooltip));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowsFilterTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => ToolTipService.SetToolTip(WindowsFilterButton, _viewModel.WindowsFilterTooltip));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.DonateTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => ToolTipService.SetToolTip(DonateButton, _viewModel.DonateTooltip));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.BugReportTooltip) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(() => ToolTipService.SetToolTip(BugReportButton, _viewModel.BugReportTooltip));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowsFilterIcon) && _viewModel != null)
        {
            DispatcherQueue.TryEnqueue(UpdateFilterButtonIcon);
        }
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
            System.Diagnostics.Debug.WriteLine($"Failed to update filter button icon: {ex.Message}");
        }
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
            System.Diagnostics.Debug.WriteLine($"Failed to update app icon: {ex.Message}");
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

            // Account for caption buttons (minimize, maximize, close)
            RightPaddingColumn.Width = new GridLength(titleBar.RightInset / scale);
            LeftPaddingColumn.Width = new GridLength(titleBar.LeftInset / scale);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set title bar padding: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to apply caption button colors: {ex.Message}");
        }
    }

    #endregion
}
