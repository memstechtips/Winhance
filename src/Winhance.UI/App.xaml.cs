using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Extensions.DI;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI;

public partial class App : Application
{
    private Window? _mainWindow;
    private IHost? _host;
    private ILogService? _logService;

    public static Window? MainWindow => (Current as App)?._mainWindow;

    public static IServiceProvider Services => (Current as App)?._host?.Services
        ?? throw new InvalidOperationException("Host not initialized");

    public App()
    {
        StartupLogger.Log("App constructor starting");
        try
        {
            // Register exception handlers before any UI initialization
            RegisterExceptionHandlers();
            StartupLogger.Log("Exception handlers registered");

            this.InitializeComponent();
            StartupLogger.Log("InitializeComponent completed");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"App constructor EXCEPTION: {ex}");
            throw;
        }
    }

    private void RegisterExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        this.UnhandledException += OnAppUnhandledException;

        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        StartupLogger.Log($"AppDomain unhandled exception: {ex?.Message}\n{ex?.StackTrace}");
        _logService?.LogError($"Fatal unhandled exception: {ex?.Message}", ex);
    }

    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupLogger.Log($"Unhandled UI exception: {e.Exception?.Message}\n{e.Exception?.StackTrace}\nInner: {e.Exception?.InnerException?.Message}\n{e.Exception?.InnerException?.StackTrace}");
        _logService?.LogError($"Unhandled UI exception: {e.Exception?.Message}", e.Exception);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupLogger.Log($"Unobserved task exception: {e.Exception?.Message}\n{e.Exception?.StackTrace}");
        _logService?.LogError($"Unobserved task exception: {e.Exception?.Message}", e.Exception);
        e.SetObserved();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupLogger.Log("OnLaunched starting");
        try
        {
            StartupLogger.Log("Building DI host...");
            _host = CompositionRoot.CreateWinhanceHost().Build();
            StartupLogger.Log("DI host built successfully");

            try
            {
                _logService = Services.GetService<ILogService>();
                StartupLogger.Log("LogService obtained");

                try
                {
                    if (_logService is Winhance.Core.Features.Common.Services.LogService concreteLogService)
                    {
                        var systemInfoProvider = Services.GetService<ISystemInfoProvider>();
                        if (systemInfoProvider != null)
                        {
                            concreteLogService.SetSystemInfoProvider(systemInfoProvider);
                        }
                    }
                    _logService?.StartLog();
                    StartupLogger.Log("LogService.StartLog() called - file logging initialized");
                    var logPath = _logService?.GetLogPath();
                    StartupLogger.Log($"Log file path: {logPath}");
                    _logService?.LogInformation("Winhance application starting...");
                }
                catch (Exception startLogEx)
                {
                    StartupLogger.Log($"StartLog() FAILED: {startLogEx.Message}");
                    StartupLogger.Log($"StartLog() Stack: {startLogEx.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                StartupLogger.Log($"LogService unavailable: {ex.Message}");
            }

            // Initialize localization before creating any UI
            StartupLogger.Log("Initializing localization...");
            InitializeLocalization();
            StartupLogger.Log("Localization initialized");

            StartupLogger.Log("Creating MainWindow...");
            _mainWindow = new MainWindow();
            StartupLogger.Log("MainWindow created, activating...");
            _mainWindow.Activate();
            StartupLogger.Log("MainWindow activated");

            // Initialize theme service after window is created
            StartupLogger.Log("Initializing theme...");
            InitializeTheme();
            StartupLogger.Log("Theme initialized");

            StartupLogger.Log("Starting startup operations...");
            (_mainWindow as MainWindow)?.StartStartupOperations();
            StartupLogger.Log("Startup operations kicked off - OnLaunched complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"OnLaunched EXCEPTION: {ex}");
            throw;
        }
    }

    private void InitializeLocalization()
    {
        try
        {
            var localizationService = Services.GetRequiredService<ILocalizationService>();

            // Load and apply the saved language preference (sync to avoid async deadlock on UI thread)
            var preferencesService = Services.GetRequiredService<IUserPreferencesService>();
            var savedLanguage = preferencesService.GetPreference("Language", "en");
            localizationService.SetLanguage(savedLanguage);
        }
        catch (Exception ex)
        {
            // Log error but don't crash - app will show key names
            _logService?.LogDebug($"Failed to initialize localization: {ex.Message}");
        }
    }

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
            _logService?.LogDebug($"Failed to load theme: {ex.Message}");
        }
    }

}
