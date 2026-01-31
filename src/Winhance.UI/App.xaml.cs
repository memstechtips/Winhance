using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Constants;
using Winhance.UI.Features.Common.Extensions.DI;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _mainWindow;
    private IHost? _host;
    private ILogService? _logService;
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "startup-debug.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [App] {message}{Environment.NewLine}");
        }
        catch { }
    }

    /// <summary>
    /// Gets the main window instance.
    /// </summary>
    public static Window? MainWindow => (Current as App)?._mainWindow;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services => (Current as App)?._host?.Services
        ?? throw new InvalidOperationException("Host not initialized");

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        Log("App constructor starting");
        try
        {
            // Register exception handlers before any UI initialization
            RegisterExceptionHandlers();
            Log("Exception handlers registered");

            this.InitializeComponent();
            Log("InitializeComponent completed");
        }
        catch (Exception ex)
        {
            Log($"App constructor EXCEPTION: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Registers all exception handlers for comprehensive error logging.
    /// </summary>
    private void RegisterExceptionHandlers()
    {
        // AppDomain unhandled exceptions (fatal errors)
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        // WinUI 3 unhandled exceptions (replaces WPF DispatcherUnhandledException)
        this.UnhandledException += OnAppUnhandledException;

        // Unobserved task exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Handles fatal AppDomain unhandled exceptions.
    /// </summary>
    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        _logService?.LogError($"Fatal unhandled exception: {ex?.Message}", ex);
        System.Diagnostics.Debug.WriteLine($"[FATAL] AppDomain unhandled exception: {ex?.Message}");
    }

    /// <summary>
    /// Handles WinUI 3 unhandled exceptions.
    /// </summary>
    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logService?.LogError($"Unhandled UI exception: {e.Exception?.Message}", e.Exception);
        System.Diagnostics.Debug.WriteLine($"[ERROR] Unhandled UI exception: {e.Exception?.Message}");
        e.Handled = true; // Prevent crash if possible
    }

    /// <summary>
    /// Handles unobserved task exceptions.
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logService?.LogError($"Unobserved task exception: {e.Exception?.Message}", e.Exception);
        System.Diagnostics.Debug.WriteLine($"[ERROR] Unobserved task exception: {e.Exception?.Message}");
        e.SetObserved(); // Prevent crash
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Log("OnLaunched starting");
        try
        {
            // Build the host with DI container using CompositionRoot
            Log("Building DI host...");
            _host = CompositionRoot.CreateWinhanceHost().Build();
            Log("DI host built successfully");

            // Initialize log service for exception handlers
            try
            {
                _logService = Services.GetService<ILogService>();
                Log("LogService obtained");
            }
            catch (Exception ex)
            {
                Log($"LogService unavailable: {ex.Message}");
            }

            // Initialize localization before creating any UI
            Log("Initializing localization...");
            InitializeLocalization();
            Log("Localization initialized");

            // Create and activate the main window
            Log("Creating MainWindow...");
            _mainWindow = new MainWindow();
            Log("MainWindow created, activating...");
            _mainWindow.Activate();
            Log("MainWindow activated");

            // Initialize theme service after window is created
            Log("Initializing theme...");
            InitializeTheme();
            Log("Theme initialized - OnLaunched complete");
        }
        catch (Exception ex)
        {
            Log($"OnLaunched EXCEPTION: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Initializes the localization system for use with x:Bind in XAML.
    /// </summary>
    private void InitializeLocalization()
    {
        try
        {
            var localizationService = Services.GetRequiredService<ILocalizationService>();
            StringKeys.Localized.Initialize(localizationService);
        }
        catch (Exception ex)
        {
            // Log error but don't crash - app will show key names
            System.Diagnostics.Debug.WriteLine($"Failed to initialize localization: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the theme system and loads the saved user preference.
    /// </summary>
    private void InitializeTheme()
    {
        try
        {
            var themeService = Services.GetRequiredService<IThemeService>();
            themeService.LoadSavedTheme();
        }
        catch (Exception ex)
        {
            // Log error but don't crash - app will use default theme
            System.Diagnostics.Debug.WriteLine($"Failed to load theme: {ex.Message}");
        }
    }
}
