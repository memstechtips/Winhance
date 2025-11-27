using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Winhance.WPF.Features.Common.Extensions.DI;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.SoftwareApps.ViewModels;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.Optimize.Views;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Customize.Views;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.WPF
{
    /// <summary>
    /// Simplified App.xaml.cs using the new composition root architecture.
    /// This class now focuses solely on application lifecycle management
    /// while delegating service configuration to the composition root.
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;
        private BackupResult? _backupResult;
        private ScriptMigrationResult? _migrationResult;
        private Mutex? _singleInstanceMutex;
        private const string MutexName = "Winhance_SingleInstance_Mutex_{B8F3E4D1-9A7C-4F2E-8D6B-1C3A5E7F9B2D}";

        /// <summary>
        /// Gets the current service provider for dependency injection.
        /// </summary>
        public IServiceProvider ServiceProvider => _host.Services;

        public App()
        {
            // DEBUG: Log constructor call with stack trace
            var stackTrace = new System.Diagnostics.StackTrace(true);
            LogStartupError($"App constructor called. Stack trace:\n{stackTrace}");

            // Check for single instance FIRST
            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);

                if (!createdNew)
                {
                    LogStartupError("Another instance of Winhance is already running");
                    // Try to activate the existing instance
                    ActivateExistingInstance();
                    Environment.Exit(0);
                    return;
                }

                LogStartupError("Single instance check passed - this is the first instance");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error during single instance check: {ex.Message}");
                // Continue anyway if mutex check fails
            }

            // Check admin privileges AFTER single instance check
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);

                LogStartupError($"Admin check - IsAdmin: {principal.IsInRole(WindowsBuiltInRole.Administrator)}");

                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    LogStartupError("Not admin - starting elevated process");

                    // Release mutex before relaunching
                    _singleInstanceMutex?.ReleaseMutex();
                    _singleInstanceMutex?.Dispose();
                    _singleInstanceMutex = null;

                    var startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("MainModule is null"),
                        Verb = "runas"
                    };

                    try
                    {
                        LogStartupError("About to start elevated process");
                        Process.Start(startInfo);
                        LogStartupError("Elevated process started, calling Environment.Exit(0)");
                        Environment.Exit(0); // Exit immediately
                    }
                    catch (System.ComponentModel.Win32Exception w32Ex) when (w32Ex.NativeErrorCode == 1223)
                    {
                        LogStartupError($"User cancelled UAC: {w32Ex.Message}");
                        Environment.Exit(1); // User cancelled
                    }
                    catch (Exception ex)
                    {
                        LogStartupError($"Error starting elevated process: {ex.Message}");
                        Environment.Exit(1); // Other error
                    }

                    LogStartupError("This should never be reached!");
                    return; // Should never reach here
                }
            }
            catch
            {
                // If admin check completely fails, continue (failsafe)
            }

            // Add global unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            try
            {
                LogStartupMessage("Creating host using composition root");
                
                // Create host using the new composition root
                _host = CompositionRoot
                    .CreateWinhanceHost()
                    .Build();

                LogStartupMessage("Application constructor completed with new DI architecture");
            }
            catch (Exception ex)
            {
                LogStartupError("Error creating host in constructor", ex);
                _host = null; // Ensure it's null if creation failed
                // Don't throw - let the app continue so we can see the error and shut down gracefully
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            LogStartupMessage("OnStartup method beginning");
            LoadingWindow? loadingWindow = null;

            try
            {
                // Defensive check - ensure host was created successfully
                if (_host == null)
                {
                    LogStartupError("Host is null - constructor may have failed");
                    Current.Shutdown();
                    return;
                }

                // Start the host and initialize services FIRST
                LogStartupMessage("Starting host with new DI architecture");
                await _host.StartAsync();
                LogStartupMessage("Host started successfully");

                // Initialize LogService after service provider is built
                await InitializeLoggingService();

                // Initialize localization service
                await InitializeLocalizationService();

                // Set application icon
                SetApplicationIcon();

                // Create and show loading window
                loadingWindow = await CreateAndShowLoadingWindow();

                // Initialize event handlers for domain events
                await InitializeEventHandlers();

                // Initialize main window and view model
                var (mainWindow, mainViewModel) = CreateMainWindow();

                // Preload application data
                await PreloadApplicationData(loadingWindow);

                // Show main window and close loading window
                ShowMainWindow(mainWindow, mainViewModel);
                CloseLoadingWindow(loadingWindow);
                loadingWindow = null;

                // Show startup notifications
                var startupNotifications = _host.Services.GetRequiredService<IStartupNotificationService>();
                await startupNotifications.ShowBackupNotificationAsync(_backupResult);
                startupNotifications.ShowMigrationNotification(_migrationResult);

                // Check for updates
                await CheckForUpdatesAsync(mainWindow);

                base.OnStartup(e);
                LogStartupMessage("OnStartup method completed successfully with new architecture");
            }
            catch (Exception ex)
            {
                LogStartupError("Error during startup with new DI architecture", ex);
                ShowStartupErrorMessage(ex);
                CloseLoadingWindow(loadingWindow);
                Current.Shutdown();
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_host != null)
                {
                    // Dispose of the ThemeManager to clean up event subscriptions
                    var themeManager = _host.Services.GetService<IThemeManager>();
                    themeManager?.Dispose();

                    using (_host)
                    {
                        await _host.StopAsync();
                    }
                }
                else
                {
                    LogStartupMessage("Host was null during shutdown - constructor likely failed");
                }

                // Release the single instance mutex
                if (_singleInstanceMutex != null)
                {
                    try
                    {
                        _singleInstanceMutex.ReleaseMutex();
                        _singleInstanceMutex.Dispose();
                        LogStartupMessage("Single instance mutex released");
                    }
                    catch (Exception mutexEx)
                    {
                        LogStartupError("Error releasing mutex", mutexEx);
                    }
                }
            }
            catch (Exception ex)
            {
                LogStartupError("Error during shutdown", ex);
            }
            finally
            {
                base.OnExit(e);
            }
        }

        #region Windows API Interop for Window Activation

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// Activates an existing instance of Winhance by finding its window and bringing it to the foreground.
        /// </summary>
        private void ActivateExistingInstance()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (var process in processes)
                {
                    // Skip the current process
                    if (process.Id == currentProcess.Id)
                        continue;

                    // Check if the process has a main window
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        IntPtr handle = process.MainWindowHandle;

                        // If the window is minimized, restore it
                        if (IsIconic(handle))
                        {
                            ShowWindow(handle, SW_RESTORE);
                        }

                        // Bring the window to the foreground
                        SetForegroundWindow(handle);

                        LogStartupError($"Activated existing Winhance window (PID: {process.Id})");
                        return;
                    }
                }

                LogStartupError("Could not find existing Winhance window to activate");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error activating existing instance: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        private async Task InitializeEventHandlers()
        {
            try
            {
                LogStartupMessage("Initializing domain event handlers");

                // Initialize the TooltipRefreshEventHandler by getting it from DI
                // This triggers its constructor which subscribes to SettingAppliedEvent
                var tooltipHandler = _host.Services.GetRequiredService<Infrastructure.Features.Common.EventHandlers.TooltipRefreshEventHandler>();

                LogStartupMessage("TooltipRefreshEventHandler initialized");
            }
            catch (Exception ex)
            {
                LogStartupError("Error initializing event handlers", ex);
            }
        }

        private async Task InitializeLoggingService()
        {
            try
            {
                var logService = _host.Services.GetService<ILogService>();
                var versionService = _host.Services.GetService<IWindowsVersionService>();

                if (logService is Winhance.Core.Features.Common.Services.LogService concreteLogService && versionService != null)
                {
                    concreteLogService.Initialize(versionService);
                    concreteLogService.StartLog();
                    LogStartupMessage("LogService initialized with IWindowsVersionService and logging started");
                }
            }
            catch (Exception initEx)
            {
                LogStartupError("Error initializing LogService", initEx);
            }
        }

        private async Task InitializeLocalizationService()
        {
            try
            {
                var localizationService = _host.Services.GetRequiredService<ILocalizationService>();
                var preferencesService = _host.Services.GetRequiredService<IUserPreferencesService>();

                WPF.Features.Common.Services.LocalizationManager.Instance.Initialize(localizationService);

                var savedLanguage = await preferencesService.GetPreferenceAsync("Language", "en");
                localizationService.SetLanguage(savedLanguage);

                LogStartupMessage($"Localization initialized with language: {savedLanguage}");
            }
            catch (Exception ex)
            {
                LogStartupError("Error initializing LocalizationService", ex);
            }
        }

        private void SetApplicationIcon()
        {
            try
            {
                var iconUri = new Uri("/Resources/AppIcons/winhance-rocket.ico", UriKind.Relative);
                Current.Resources["ApplicationIcon"] = new System.Windows.Media.Imaging.BitmapImage(iconUri);
                LogStartupMessage("Application icon set successfully");
            }
            catch (Exception iconEx)
            {
                LogStartupError("Failed to set application icon", iconEx);
            }
        }

        private async Task<LoadingWindow> CreateAndShowLoadingWindow()
        {
            LogStartupMessage("Creating loading window");
            var themeManager = _host.Services.GetRequiredService<IThemeManager>();
            var progressService = _host.Services.GetRequiredService<ITaskProgressService>();

            // Ensure the IsDarkTheme resource is set
            Application.Current.Resources["IsDarkTheme"] = themeManager.IsDarkTheme;
            LogStartupMessage($"Set IsDarkTheme resource to {themeManager.IsDarkTheme}");

            var loadingWindow = new LoadingWindow(themeManager, progressService);
            loadingWindow.Show();
            LogStartupMessage("Loading window shown");

            return loadingWindow;
        }

        private (MainWindow mainWindow, MainViewModel mainViewModel) CreateMainWindow()
        {
            LogStartupMessage("Getting main window and view model");
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();

            mainWindow.DataContext = mainViewModel;
            Application.Current.MainWindow = mainWindow;

            LogStartupMessage("Main window and view model initialized");
            return (mainWindow, mainViewModel);
        }

        private async Task PreloadApplicationData(LoadingWindow? loadingWindow)
        {
            try
            {
                LogStartupMessage("Initializing compatible settings registry");
                var settingsRegistry = _host.Services.GetRequiredService<ICompatibleSettingsRegistry>();
                await settingsRegistry.InitializeAsync();
                LogStartupMessage("Compatible settings registry initialized");

                LogStartupMessage("Preloading global settings registry");
                var settingsPreloader = _host.Services.GetRequiredService<IGlobalSettingsPreloader>();
                await settingsPreloader.PreloadAllSettingsAsync();
                LogStartupMessage("Global settings registry preloaded");

                LogStartupMessage("Checking system backup preferences");
                var prefsService = _host.Services.GetRequiredService<IUserPreferencesService>();
                var skipBackup = await prefsService.GetPreferenceAsync("SkipSystemBackup", false);
                var registryBackupCompleted = await prefsService.GetPreferenceAsync("RegistryBackupCompleted", false);

                if (!skipBackup || !registryBackupCompleted)
                {
                    LogStartupMessage("Creating initial system backups");
                    var backupService = _host.Services.GetRequiredService<ISystemBackupService>();
                    _backupResult = await backupService.EnsureInitialBackupsAsync();
                    LogStartupMessage($"Backup operation completed. Success: {_backupResult.Success}");
                }
                else
                {
                    LogStartupMessage("System backup skipped - user has disabled this feature and registry backup is complete");
                }

                LogStartupMessage("Checking for legacy script paths");
                var migrationService = _host.Services.GetRequiredService<IScriptMigrationService>();
                _migrationResult = await migrationService.MigrateFromOldPathsAsync();

                if (_migrationResult.MigrationPerformed)
                {
                    LogStartupMessage("Script migration completed");
                }
                else
                {
                    LogStartupMessage("No script migration needed");
                }

                var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();

                LogStartupMessage("Preloading SoftwareAppsViewModel data");
                var softwareAppsViewModel = _host.Services.GetRequiredService<SoftwareAppsViewModel>();

                await softwareAppsViewModel.InitializeCommand.ExecuteAsync(null);

                LogStartupMessage("SoftwareAppsViewModel fully preloaded with installation status");

                LogStartupMessage("Navigating to default view (SoftwareApps)");
                await mainViewModel.InitializeApplicationAsync();
                LogStartupMessage("Navigation completed and UI ready");
            }
            catch (Exception ex)
            {
                LogStartupError("Error preloading application data", ex);
                throw;
            }
        }

        private void ShowMainWindow(MainWindow mainWindow, MainViewModel mainViewModel)
        {
            LogStartupMessage("Initializing and showing main window");

            // Initialize window with effects and messaging
            var windowInitService = _host.Services.GetRequiredService<WindowInitializationService>();
            windowInitService.InitializeWindow(mainWindow);

            mainWindow.Show();
            LogStartupMessage("Main window shown");
        }

        private static void CloseLoadingWindow(LoadingWindow? loadingWindow)
        {
            if (loadingWindow != null)
            {
                loadingWindow.Close();
                LogStartupMessage("Loading window closed");
            }
        }

        private async Task CheckForUpdatesAsync(Window ownerWindow)
        {
            try
            {
                LogStartupMessage("Checking for updates...");
                var versionService = _host.Services.GetRequiredService<IVersionService>();
                var latestVersion = await versionService.CheckForUpdateAsync();

                if (latestVersion.IsUpdateAvailable)
                {
                    LogStartupMessage($"Update available: {latestVersion.Version}");
                    await ShowUpdateDialog(versionService, latestVersion);
                }
                else
                {
                    LogStartupMessage("No updates available");
                }
            }
            catch (Exception ex)
            {
                LogStartupError($"Error checking for updates", ex);
            }
        }

        private async Task ShowUpdateDialog(IVersionService versionService, VersionInfo latestVersion)
        {
            var currentVersion = versionService.GetCurrentVersion();
            string message = "Good News! A New Version of Winhance is available.";

            Func<Task> downloadAndInstallAction = async () =>
            {
                await versionService.DownloadAndInstallUpdateAsync();
                System.Windows.Application.Current.Shutdown();
            };

            bool installNow = await UpdateDialog.ShowAsync(
                "Update Available",
                message,
                currentVersion,
                latestVersion,
                downloadAndInstallAction
            );

            LogStartupMessage(installNow
                ? "User chose to download and install the update"
                : "User chose to be reminded later");
        }

        #endregion

        #region Exception Handlers

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var ex = args.ExceptionObject as Exception;
            LogStartupError($"Unhandled AppDomain exception: {ex?.Message}", ex);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
        {
            LogStartupError($"Unhandled Dispatcher exception: {args.Exception.Message}", args.Exception);
            args.Handled = true; // Prevent the application from crashing
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            LogStartupError($"Unobserved Task exception: {args.Exception.Message}", args.Exception);
            args.SetObserved(); // Prevent the application from crashing
        }

        #endregion

        #region Logging Methods

        private static void LogStartupMessage(string message)
        {
            LogStartupError(message);
        }

        private static void LogStartupError(string message, Exception? ex = null)
        {
            string fullMessage = $"[{DateTime.Now}] {message}";
            if (ex != null)
            {
                fullMessage += $"\nException: {ex.Message}\nStack Trace: {ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    fullMessage += $"\nInner Exception: {ex.InnerException.Message}";
                }
            }

            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Winhance",
                    "Logs",
                    "WinhanceStartupLog.txt"
                );

                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"{fullMessage}\n");
            }
            catch
            {
                // Silently fail if logging is not possible
            }
        }

        private static void ShowStartupErrorMessage(Exception ex)
        {
            MessageBox.Show(
                $"Error during startup: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        #endregion
    }
}