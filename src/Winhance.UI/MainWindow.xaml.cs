using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Customize;
using Winhance.UI.Features.Optimize;
using Winhance.UI.Features.Settings;
using Winhance.UI.Features.SoftwareApps;

namespace Winhance.UI;

/// <summary>
/// Main application window with NavigationView.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "startup-debug.log");

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

        // Set initial window size
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));

        // Apply Mica backdrop (Windows 11) with fallback to DesktopAcrylic (Windows 10)
        TrySetMicaBackdrop();

        // Initialize DispatcherService - MUST be done before any service uses it
        // This is required because DispatcherQueue is only available after window creation
        InitializeDispatcherService();

        // Set default navigation to SoftwareApps after window is loaded
        NavView.Loaded += NavView_Loaded;
    }

    /// <summary>
    /// Sets the default navigation item after the NavigationView is loaded.
    /// </summary>
    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        Log($"NavView_Loaded - MenuItems count: {NavView.MenuItems.Count}");
        // Navigate to Settings page by default (for testing - has fewer dependencies)
        // TODO: Change back to SoftwareApps when all services are registered
        Log("Navigating to Settings as default...");
        NavView.SelectedItem = NavView.SettingsItem;
        Log("Settings selected");
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
    /// Handles navigation when a NavigationViewItem is selected.
    /// </summary>
    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Log($"NavView_SelectionChanged - IsSettingsSelected: {args.IsSettingsSelected}");
        if (args.IsSettingsSelected)
        {
            NavigateToPage("Settings");
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            Log($"Selected item tag: {tag}");
            NavigateToPage(tag);
        }
        else
        {
            Log("SelectedItemContainer is null or not NavigationViewItem");
        }
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
}
