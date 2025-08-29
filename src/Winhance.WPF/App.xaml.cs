using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Winhance.WPF.Features.Common.Extensions.DI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

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

        /// <summary>
        /// Gets the current service provider for dependency injection.
        /// </summary>
        public IServiceProvider ServiceProvider => _host.Services;

        public App()
        {
            // Add global unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // Create host using the new composition root
            _host = CompositionRoot
                .CreateWinhanceHost()
                .Build();

            LogStartupMessage("Application constructor completed with new DI architecture");
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            LogStartupMessage("OnStartup method beginning");
            LoadingWindow? loadingWindow = null;

            try
            {
                // Initialize LogService after service provider is built
                await InitializeLoggingService();

                // Ensure administrator privileges
                await EnsureAdministratorPrivileges();

                // Set application icon
                SetApplicationIcon();

                // Create and show loading window
                loadingWindow = await CreateAndShowLoadingWindow();

                // Start the host and initialize services
                LogStartupMessage("Starting host with new DI architecture");
                await _host.StartAsync();
                LogStartupMessage("Host started successfully");

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
                // Dispose of the ThemeManager to clean up event subscriptions
                var themeManager = _host.Services.GetService<IThemeManager>();
                themeManager?.Dispose();

                using (_host)
                {
                    await _host.StopAsync();
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
                var systemServices = _host.Services.GetService<ISystemServices>();

                if (logService is Winhance.Core.Features.Common.Services.LogService concreteLogService && systemServices != null)
                {
                    concreteLogService.Initialize(systemServices);
                    concreteLogService.StartLog();
                    LogStartupMessage("LogService initialized with ISystemServices and logging started");
                }
            }
            catch (Exception initEx)
            {
                LogStartupError("Error initializing LogService", initEx);
            }
        }

        private async Task EnsureAdministratorPrivileges()
        {
            try
            {
                LogStartupMessage("Checking for administrator privileges");
                var systemServices = _host.Services.GetService<ISystemServices>();

                if (systemServices != null)
                {
                    bool isAdmin = systemServices.RequireAdministrator();
                    LogStartupMessage($"Administrator privileges check result: {isAdmin}");
                }
                else
                {
                    LogStartupMessage("ISystemServices not available for admin check");
                }
            }
            catch (Exception adminEx)
            {
                LogStartupError("Error checking administrator privileges", adminEx);
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
            var taskProgressService = _host.Services.GetRequiredService<ITaskProgressService>();
            var progressHandler = CreateProgressHandler(loadingWindow);

            taskProgressService.ProgressChanged += progressHandler;

            try
            {
                // Preload WindowsAppsViewModel data
                LogStartupMessage("Loading WindowsAppsViewModel data");
                var windowsAppsViewModel = _host.Services.GetRequiredService<WindowsAppsViewModel>();
                await windowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
                LogStartupMessage("WindowsApps loaded and installation status checked");

                // Preload SoftwareAppsViewModel data  
                LogStartupMessage("Initializing SoftwareAppsViewModel");
                var softwareAppsViewModel = _host.Services.GetRequiredService<SoftwareAppsViewModel>();
                await softwareAppsViewModel.InitializeCommand.ExecuteAsync(null);
                LogStartupMessage("SoftwareAppsViewModel initialized");
            }
            finally
            {
                taskProgressService.ProgressChanged -= progressHandler;
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

        private static EventHandler<TaskProgressEventArgs> CreateProgressHandler(LoadingWindow? loadingWindow)
        {
            return (sender, args) =>
            {
                if (loadingWindow?.DataContext is LoadingWindowViewModel vm)
                {
                    vm.Progress = args.Progress * 100;
                    vm.StatusMessage = args.StatusText;
                    vm.IsIndeterminate = args.IsIndeterminate;
                    vm.ShowProgressText = !args.IsIndeterminate && args.IsTaskRunning;

                    // Update detail message based on current operation
                    vm.DetailMessage = args.StatusText switch
                    {
                        var text when text.Contains("Loading installable apps") => "Discovering available applications...",
                        var text when text.Contains("Loading removable apps") => "Identifying Windows applications...",
                        var text when text.Contains("Checking installation status") => "Verifying which applications are installed...",
                        var text when text.Contains("Organizing apps") => "Sorting applications for display...",
                        _ => vm.DetailMessage
                    };
                }
            };
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
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Winhance",
                    "Logs",
                    "WinhanceStartupLog.txt"
                );

                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"{fullMessage}\n");
            }
            catch
            {
                // If we can't log to file, show a message box as fallback
                MessageBox.Show(fullMessage, "Startup Message", MessageBoxButton.OK, MessageBoxImage.Information);
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