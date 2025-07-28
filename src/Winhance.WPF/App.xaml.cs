using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Optimize.Services;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.UI.Interfaces;
using Winhance.Infrastructure.Features.Common.Registry;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Customize.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Implementations;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification.Methods;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Services.Configuration;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Customize.Views;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.Optimize.Views;
using Winhance.WPF.Features.SoftwareApps.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Views;

namespace Winhance.WPF
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            // Add global unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogStartupError($"Unhandled AppDomain exception: {ex?.Message}", ex);
            };

            Current.DispatcherUnhandledException += (sender, args) =>
            {
                LogStartupError(
                    $"Unhandled Dispatcher exception: {args.Exception.Message}",
                    args.Exception
                );
                args.Handled = true; // Prevent the application from crashing
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogStartupError(
                    $"Unobserved Task exception: {args.Exception.Message}",
                    args.Exception
                );
                args.SetObserved(); // Prevent the application from crashing
            };

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(
                    (context, services) =>
                    {
                        ConfigureServices(services);
                    }
                )
                .Build();

            LogStartupError("Application constructor completed");
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            LogStartupError("OnStartup method beginning");
            LoadingWindow? loadingWindow = null;

            // Ensure the application has administrator privileges
            try
            {
                LogStartupError("Checking for administrator privileges");
                var systemServices = _host.Services.GetService<ISystemServices>();
                if (systemServices != null)
                {
                    bool isAdmin = systemServices.RequireAdministrator();
                    LogStartupError($"Administrator privileges check result: {isAdmin}");
                }
                else
                {
                    LogStartupError("ISystemServices not available for admin check");
                }
            }
            catch (Exception adminEx)
            {
                LogStartupError(
                    "Error checking administrator privileges: " + adminEx.Message,
                    adminEx
                );
            }

            // Set the application icon for all windows
            try
            {
                // We'll continue to use the original icon for the application icon (taskbar, shortcuts, etc.)
                var iconUri = new Uri("/Resources/AppIcons/winhance-rocket.ico", UriKind.Relative);
                Current.Resources["ApplicationIcon"] = new System.Windows.Media.Imaging.BitmapImage(
                    iconUri
                );
                LogStartupError("Application icon set successfully");
            }
            catch (Exception iconEx)
            {
                LogStartupError("Failed to set application icon: " + iconEx.Message, iconEx);
            }

            try
            {
                // Create and show loading window first
                LogStartupError("Attempting to get theme manager");
                var themeManager = _host.Services.GetRequiredService<IThemeManager>();
                LogStartupError("Got theme manager");

                // Ensure the IsDarkTheme resource is set in the application resources
                Application.Current.Resources["IsDarkTheme"] = themeManager.IsDarkTheme;
                LogStartupError($"Set IsDarkTheme resource to {themeManager.IsDarkTheme}");

                LogStartupError("Attempting to get task progress service");
                var initialProgressService =
                    _host.Services.GetRequiredService<ITaskProgressService>();
                LogStartupError("Got task progress service");

                LogStartupError("Creating loading window");
                loadingWindow = new LoadingWindow(themeManager, initialProgressService);
                LogStartupError("Loading window created");

                LogStartupError("Showing loading window");
                loadingWindow.Show();
                LogStartupError("Loading window shown");

                // Start the host and initialize services
                LogStartupError("Starting host");
                await _host.StartAsync();
                LogStartupError("Host started");

                // Get required services
                LogStartupError("Getting main window");
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                LogStartupError("Got main window");

                LogStartupError("Getting main view model");
                var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
                LogStartupError("Got main view model");

                // Set the MainWindow's DataContext and make it the application's main window
                LogStartupError("Setting MainWindow DataContext");
                mainWindow.DataContext = mainViewModel;
                LogStartupError("MainWindow DataContext set");

                LogStartupError("Setting Application.Current.MainWindow");
                Application.Current.MainWindow = mainWindow;
                LogStartupError("Application.Current.MainWindow set");

                LogStartupError("Getting software apps view model");
                try
                {
                    // Log each dependency to see which one might be causing issues
                    LogStartupError("Getting ITaskProgressService for WindowsAppsViewModel");
                    var progressService = _host.Services.GetRequiredService<ITaskProgressService>();
                    LogStartupError("Got ITaskProgressService");

                    LogStartupError("Getting ISearchService for WindowsAppsViewModel");
                    var searchService = _host.Services.GetRequiredService<ISearchService>();
                    LogStartupError("Got ISearchService");

                    LogStartupError("Getting IPackageManager for WindowsAppsViewModel");

                    // Declare packageManager variable here so it's in scope for the entire method
                    IPackageManager packageManager;

                    // Try resolving each dependency of PackageManager individually
                    try
                    {
                        LogStartupError("Resolving ILogService");
                        var debugLogService = _host.Services.GetRequiredService<ILogService>();
                        LogStartupError("ILogService resolved successfully");

                        LogStartupError("Resolving IAppService");
                        var debugAppService = _host.Services.GetRequiredService<IAppService>();
                        LogStartupError("IAppService resolved successfully");

                        LogStartupError("Resolving IAppRemovalService");
                        var debugAppRemovalService =
                            _host.Services.GetRequiredService<IAppRemovalService>();
                        LogStartupError("IAppRemovalService resolved successfully");

                        LogStartupError("Resolving ICapabilityRemovalService");
                        var debugCapabilityRemovalService =
                            _host.Services.GetRequiredService<ICapabilityRemovalService>();
                        LogStartupError("ICapabilityRemovalService resolved successfully");

                        LogStartupError("Resolving IFeatureRemovalService");
                        var debugFeatureRemovalService =
                            _host.Services.GetRequiredService<IFeatureRemovalService>();
                        LogStartupError("IFeatureRemovalService resolved successfully");

                        LogStartupError("Resolving ISpecialAppHandlerService");
                        var debugSpecialAppHandlerService =
                            _host.Services.GetRequiredService<ISpecialAppHandlerService>();
                        LogStartupError("ISpecialAppHandlerService resolved successfully");

                        LogStartupError("Resolving IBloatRemovalScriptService");
                        var debugBloatRemovalScriptService =
                            _host.Services.GetRequiredService<IBloatRemovalScriptService>();
                        LogStartupError("IBloatRemovalScriptService resolved successfully");

                        LogStartupError("Resolving ISystemServices");
                        var debugSystemServices =
                            _host.Services.GetRequiredService<ISystemServices>();
                        LogStartupError("ISystemServices resolved successfully");

                        LogStartupError("Resolving INotificationService");
                        var debugNotificationService =
                            _host.Services.GetRequiredService<INotificationService>();
                        LogStartupError("INotificationService resolved successfully");

                        // Now try to resolve IPackageManager
                        LogStartupError("Now resolving IPackageManager");
                        packageManager = _host.Services.GetRequiredService<IPackageManager>();
                        LogStartupError("Got IPackageManager");
                    }
                    catch (Exception ex)
                    {
                        LogStartupError($"Error resolving dependency: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            LogStartupError($"Inner exception: {ex.InnerException.Message}");
                        }
                        throw;
                    }

                    // After we've verified IPackageManager can be resolved, get the remaining dependencies
                    LogStartupError("Getting IAppInstallationService for WindowsAppsViewModel");
                    var appInstallationService =
                        _host.Services.GetRequiredService<IAppInstallationService>();
                    LogStartupError("Got IAppInstallationService");

                    LogStartupError(
                        "Getting ICapabilityInstallationService for WindowsAppsViewModel"
                    );
                    var capabilityService =
                        _host.Services.GetRequiredService<ICapabilityInstallationService>();
                    LogStartupError("Got ICapabilityInstallationService");

                    LogStartupError("Getting IFeatureInstallationService for WindowsAppsViewModel");
                    var featureService =
                        _host.Services.GetRequiredService<IFeatureInstallationService>();
                    LogStartupError("Got IFeatureInstallationService");

                    LogStartupError("Getting IFeatureRemovalService for WindowsAppsViewModel");
                    var featureRemovalService =
                        _host.Services.GetRequiredService<IFeatureRemovalService>();
                    LogStartupError("Got IFeatureRemovalService");

                    LogStartupError("Getting IConfigurationService for WindowsAppsViewModel");
                    var configurationService =
                        _host.Services.GetRequiredService<IConfigurationService>();
                    LogStartupError("Got IConfigurationService");

                    LogStartupError("Getting IScriptDetectionService for WindowsAppsViewModel");
                    var scriptDetectionService =
                        _host.Services.GetRequiredService<IScriptDetectionService>();
                    LogStartupError("Got IScriptDetectionService");

                    LogStartupError(
                        "Getting IInternetConnectivityService for WindowsAppsViewModel"
                    );
                    var connectivityService =
                        _host.Services.GetRequiredService<IInternetConnectivityService>();
                    LogStartupError("Got IInternetConnectivityService");

                    LogStartupError(
                        "Getting IAppInstallationCoordinatorService for WindowsAppsViewModel"
                    );
                    var appInstallationCoordinatorService =
                        _host.Services.GetRequiredService<IAppInstallationCoordinatorService>();
                    LogStartupError("Got IAppInstallationCoordinatorService");

                    LogStartupError(
                        "Getting IBloatRemovalCoordinatorService for WindowsAppsViewModel"
                    );
                    var bloatRemovalCoordinatorService =
                        _host.Services.GetRequiredService<IBloatRemovalCoordinatorService>();
                    LogStartupError("Got IBloatRemovalCoordinatorService");

                    LogStartupError("Getting SoftwareAppsDialogService for WindowsAppsViewModel");
                    var dialogService =
                        _host.Services.GetRequiredService<Features.SoftwareApps.Services.SoftwareAppsDialogService>();
                    LogStartupError("Got SoftwareAppsDialogService");

                    LogStartupError("Now creating WindowsAppsViewModel with all dependencies");
                    // Just test creating the object but don't assign it
                    new WindowsAppsViewModel(
                        progressService,
                        searchService,
                        packageManager,
                        appInstallationService,
                        capabilityService,
                        featureService,
                        featureRemovalService,
                        configurationService,
                        scriptDetectionService,
                        connectivityService,
                        appInstallationCoordinatorService,
                        bloatRemovalCoordinatorService,
                        dialogService,
                        _host.Services
                    );
                    LogStartupError("Successfully created WindowsAppsViewModel test instance");
                }
                catch (Exception ex)
                {
                    LogStartupError($"Error creating WindowsAppsViewModel: {ex.Message}", ex);
                    throw;
                }

                // Now get the service from DI
                LogStartupError("Getting WindowsAppsViewModel from DI");
                var windowsAppsViewModel =
                    _host.Services.GetRequiredService<WindowsAppsViewModel>();
                LogStartupError("Got Windows apps view model from DI");

                // Set the DataContext
                LogStartupError("Setting main window DataContext");
                mainWindow.DataContext = mainViewModel;
                LogStartupError("Main window DataContext set");

                // Initialize window with effects and messaging
                LogStartupError("Initializing window");
                var windowInitService = _host.Services.GetRequiredService<Features.Common.Services.WindowInitializationService>();
                windowInitService.InitializeWindow(mainWindow);
                LogStartupError("Window initialized");

                // Preload the SoftwareAppsViewModel data
                LogStartupError("Loading apps and checking installation status...");

                // Create a progress handler to update the loading window
                var progressHandler = new EventHandler<TaskProgressEventArgs>(
                    (sender, args) =>
                    {
                        if (
                            loadingWindow != null
                            && loadingWindow.DataContext is LoadingWindowViewModel vm
                        )
                        {
                            vm.Progress = args.Progress * 100;
                            vm.StatusMessage = args.StatusText;
                            vm.IsIndeterminate = args.IsIndeterminate;
                            vm.ShowProgressText = !args.IsIndeterminate && args.IsTaskRunning;

                            // Update detail message based on the current operation
                            if (args.StatusText.Contains("Loading installable apps"))
                            {
                                vm.DetailMessage = "Discovering available applications...";
                            }
                            else if (args.StatusText.Contains("Loading removable apps"))
                            {
                                vm.DetailMessage = "Identifying Windows applications...";
                            }
                            else if (args.StatusText.Contains("Checking installation status"))
                            {
                                vm.DetailMessage = "Verifying which applications are installed...";
                            }
                            else if (args.StatusText.Contains("Organizing apps"))
                            {
                                vm.DetailMessage = "Sorting applications for display...";
                            }
                        }
                    }
                );

                // Subscribe to progress events
                LogStartupError("Getting task progress service");
                var taskProgressService = _host.Services.GetRequiredService<ITaskProgressService>();
                LogStartupError("Got task progress service");

                LogStartupError("Subscribing to progress events");
                taskProgressService.ProgressChanged += progressHandler;
                LogStartupError("Subscribed to progress events");

                try
                {
                    // Load apps and check installation status while showing the loading screen
                    LogStartupError(
                        "Starting LoadAppsAndCheckInstallationStatusAsync for WindowsAppsViewModel"
                    );
                    await windowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
                    LogStartupError("WindowsApps loaded and installation status checked");

                    // Also preload the SoftwareAppsViewModel data
                    LogStartupError("Getting SoftwareAppsViewModel");
                    var softwareAppsViewModel =
                        _host.Services.GetRequiredService<SoftwareAppsViewModel>();
                    LogStartupError("Got SoftwareAppsViewModel");

                    LogStartupError("Starting Initialize for SoftwareAppsViewModel");
                    await softwareAppsViewModel.InitializeCommand.ExecuteAsync(null);
                    LogStartupError("SoftwareAppsViewModel initialized");

                    // Preload the OptimizeViewModel to ensure all registry settings are loaded
                    LogStartupError("Getting OptimizeViewModel");
                    var optimizeViewModel = _host.Services.GetRequiredService<OptimizeViewModel>();
                    LogStartupError("Got OptimizeViewModel");

                    // Initialize the OptimizeViewModel and wait for it to complete
                    LogStartupError("Starting Initialize for OptimizeViewModel");
                    if (loadingWindow?.DataContext is LoadingWindowViewModel loadingVM)
                    {
                        loadingVM.StatusMessage = "Loading system settings...";
                        loadingVM.DetailMessage = "Checking registry settings...";
                    }
                    await optimizeViewModel.InitializeCommand.ExecuteAsync(null);
                    LogStartupError("OptimizeViewModel initialized");

                    // Preload the CustomizeViewModel to ensure all customization settings are loaded
                    LogStartupError("Getting CustomizeViewModel");
                    var customizeViewModel =
                        _host.Services.GetRequiredService<CustomizeViewModel>();
                    LogStartupError("Got CustomizeViewModel");

                    // Initialize the CustomizeViewModel and wait for it to complete
                    LogStartupError("Starting Initialize for CustomizeViewModel");
                    if (loadingWindow?.DataContext is LoadingWindowViewModel loadingVMCustomize)
                    {
                        loadingVMCustomize.StatusMessage = "Loading customization settings...";
                        loadingVMCustomize.DetailMessage = "Checking customization options...";
                    }
                    await customizeViewModel.InitializeCommand.ExecuteAsync(null);
                    LogStartupError("CustomizeViewModel initialized");
                }
                finally
                {
                    // Unsubscribe from progress events
                    LogStartupError("Unsubscribing from progress events");
                    taskProgressService.ProgressChanged -= progressHandler;
                    LogStartupError("Unsubscribed from progress events");
                }

                // Show the main window
                LogStartupError("Showing main window");
                mainWindow.Show();
                LogStartupError("Main window shown");

                // Close the loading window
                LogStartupError("Closing loading window");
                loadingWindow.Close();
                loadingWindow = null;
                LogStartupError("Loading window closed");

                // Check for updates
                await CheckForUpdatesAsync(mainWindow);

                LogStartupError("Calling base.OnStartup");
                base.OnStartup(e);
                LogStartupError("OnStartup method completed successfully");
            }
            catch (Exception ex)
            {
                LogStartupError("Error during startup", ex);
                MessageBox.Show(
                    $"Error during startup: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                // Ensure loading window is closed if there was an error
                if (loadingWindow != null)
                {
                    loadingWindow.Close();
                }

                Current.Shutdown();
            }
        }

        private async Task CheckForUpdatesAsync(Window ownerWindow)
        {
            try
            {
                LogStartupError("Checking for updates...");
                var versionService = _host.Services.GetRequiredService<IVersionService>();
                var latestVersion = await versionService.CheckForUpdateAsync();

                if (latestVersion.IsUpdateAvailable)
                {
                    LogStartupError($"Update available: {latestVersion.Version}");

                    // Get current version
                    var currentVersion = versionService.GetCurrentVersion();

                    // Show the styled update dialog
                    string message = "Good News! A New Version of Winhance is available.";

                    // Create a download and install action
                    Func<Task> downloadAndInstallAction = async () =>
                    {
                        await versionService.DownloadAndInstallUpdateAsync();

                        // Close the application without showing a message
                        System.Windows.Application.Current.Shutdown();
                    };

                    // Show the update dialog
                    bool installNow = await UpdateDialog.ShowAsync(
                        "Update Available",
                        message,
                        currentVersion,
                        latestVersion,
                        downloadAndInstallAction
                    );

                    if (installNow)
                    {
                        LogStartupError("User chose to download and install the update");
                    }
                    else
                    {
                        LogStartupError("User chose to be reminded later");
                    }
                }
                else
                {
                    LogStartupError("No updates available");
                }
            }
            catch (Exception ex)
            {
                LogStartupError($"Error checking for updates: {ex.Message}", ex);
                // Don't show error to user, just log it
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                // Material Design resources are automatically unloaded

                // Dispose of the ThemeManager to clean up event subscriptions
                try
                {
                    var themeManager = _host.Services.GetService<IThemeManager>();
                    themeManager?.Dispose();
                }
                catch (Exception disposeEx)
                {
                    LogStartupError("Error disposing ThemeManager", disposeEx);
                }

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

        private void LogStartupError(string message, Exception? ex = null)
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

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, $"{fullMessage}\n");
            }
            catch
            {
                MessageBox.Show(
                    fullMessage,
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            try
            {
                LogStartupError("Beginning service configuration...");

                // Core Services
                services.AddSingleton<ILogService, LogService>();
                services.AddSingleton<IRegistryService, RegistryService>();
                services.AddSingleton<ICommandService, CommandService>();
                services.AddSingleton<IDependencyManager, DependencyManager>();
                services.AddSingleton<ISettingsRegistry, SettingsRegistry>();
                services.AddSingleton<IViewModelLocator, ViewModelLocator>();
                services.AddSingleton<IBatteryService, BatteryService>();
                services.AddSingleton<IScriptPathService, ScriptPathService>();
                services.AddSingleton<
                    Winhance.Core.Interfaces.Services.IFileSystemService,
                    Winhance.Infrastructure.FileSystem.FileSystemService
                >();

                // Register the base services
                services.AddSingleton<AppDiscoveryService>();
                services.AddSingleton<IAppDiscoveryService>(provider =>
                    provider.GetRequiredService<AppDiscoveryService>()
                );
                services.AddSingleton<IAppService, AppServiceAdapter>();
                services.AddSingleton<ISpecialAppHandlerService>(
                    provider => new SpecialAppHandlerService(
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<ISystemServices>()
                    )
                );
                services.AddSingleton<
                    ISearchService,
                    Winhance.Infrastructure.Features.Common.Services.SearchService
                >();
                services.AddSingleton<
                    IConfigurationService,
                    Winhance.Infrastructure.Features.Common.Services.ConfigurationService
                >();
                // Register UserPreferencesService as a singleton to ensure the same instance is used throughout the app
                services.AddSingleton<Features.Common.Services.UserPreferencesService>(provider =>
                {
                    var logService = provider.GetRequiredService<ILogService>();
                    var userPreferencesService =
                        new Features.Common.Services.UserPreferencesService(logService);

                    // Log that the UserPreferencesService has been registered
                    logService.Log(LogLevel.Info, "UserPreferencesService registered as singleton");

                    return userPreferencesService;
                });
                services.AddSingleton<IUnifiedConfigurationService>(
                    provider => new Winhance.WPF.Features.Common.Services.UnifiedConfigurationService(
                        provider.GetRequiredService<IServiceProvider>(),
                        provider.GetRequiredService<IConfigurationService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<Core.Features.Common.Interfaces.IDialogService>(),
                        provider.GetRequiredService<IRegistryService>()
                    )
                );

                // Register configuration services
                services.AddConfigurationServices();

                // Keep the old service for backward compatibility during transition
                services.AddSingleton<IConfigurationCoordinatorService>(
                    provider => new Winhance.WPF.Features.Common.Services.ConfigurationCoordinatorService(
                        provider.GetRequiredService<IServiceProvider>(),
                        provider.GetRequiredService<IConfigurationService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<Core.Features.Common.Interfaces.IDialogService>(),
                        provider.GetRequiredService<IRegistryService>()
                    )
                );
                services.AddSingleton<IScriptDetectionService>(sp =>
                    new Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration.ScriptDetectionService(
                        sp.GetRequiredService<IScriptPathService>()
                    )
                );

                // Register script generation services
                services.AddScriptGenerationServices();

                // Register bloat removal services
                services.AddBloatRemovalServices();

                services.AddSingleton<IPowerShellExecutionService>(
                    provider => new PowerShellExecutionService(
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<ISystemServices>()
                    )
                );

                // Register power-related services
                services.AddSingleton<
                    Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService,
                    Winhance.Infrastructure.Features.Optimize.Services.PowerPlanService
                >();
                
                // Register power setting service
                services.AddSingleton<
                    IPowerSettingService,
                    Winhance.Infrastructure.Features.Optimize.Services.PowerSettingService
                >();
                
                // Register power plan manager service
                services.AddSingleton<
                    IPowerPlanManagerService,
                    Winhance.Infrastructure.Features.Optimize.Services.PowerPlanManagerService
                >();

                // Register the installation and removal services
                // Register WinGet verification methods
                services.AddSingleton<IVerificationMethod, WinGetVerificationMethod>();
                services.AddSingleton<IVerificationMethod, RegistryVerificationMethod>();
                services.AddSingleton<IVerificationMethod, AppxPackageVerificationMethod>();
                services.AddSingleton<IVerificationMethod, FileSystemVerificationMethod>();

                // Register the composite verifier
                services.AddSingleton<IInstallationVerifier, CompositeInstallationVerifier>();

                // Register the WinGet installer
                services.AddSingleton<IWinGetInstaller, WinGetInstaller>();

                // Register the adapter for backward compatibility
                services.AddSingleton<
                    IWinGetInstallationService,
                    WinGetInstallationServiceAdapter
                >();

                // Register the main installation service
                services.AddSingleton<IAppInstallationService>(
                    provider => new AppInstallationService(
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<IPowerShellExecutionService>(),
                        provider.GetRequiredService<IScriptUpdateService>(),
                        provider.GetRequiredService<ISystemServices>(),
                        provider.GetRequiredService<IWinGetInstallationService>()
                    )
                );
                services.AddSingleton<IAppRemovalService>(provider => new AppRemovalService(
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<ISpecialAppHandlerService>(),
                    provider.GetRequiredService<IAppDiscoveryService>(),
                    provider.GetRequiredService<IBloatRemovalScriptService>(),
                    provider.GetRequiredService<ISystemServices>(),
                    provider.GetRequiredService<IRegistryService>()
                ));
                services.AddSingleton<
                    ICapabilityInstallationService,
                    CapabilityInstallationService
                >();
                services.AddSingleton<ICapabilityRemovalService>(
                    provider => new CapabilityRemovalService(
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<IAppDiscoveryService>(),
                        provider.GetRequiredService<IBloatRemovalScriptService>()
                    )
                );
                services.AddSingleton<IFeatureInstallationService, FeatureInstallationService>();
                services.AddSingleton<IFeatureRemovalService>(provider => new FeatureRemovalService(
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IAppDiscoveryService>(),
                    provider.GetRequiredService<IBloatRemovalScriptService>()
                ));

                // Register the orchestrator
                services.AddSingleton<InstallationOrchestrator>();

                // Register the PackageManager with its dependencies
                services.AddSingleton<IPackageManager>(provider => new PackageManager(
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IAppService>(),
                    provider.GetRequiredService<IAppRemovalService>(),
                    provider.GetRequiredService<ICapabilityRemovalService>(),
                    provider.GetRequiredService<IFeatureRemovalService>(),
                    provider.GetRequiredService<ISpecialAppHandlerService>(),
                    provider.GetRequiredService<IBloatRemovalScriptService>(),
                    provider.GetRequiredService<ISystemServices>(),
                    provider.GetService<INotificationService>()
                ));

                // Register the InternetConnectivityService
                services.AddSingleton<IInternetConnectivityService>(
                    provider => new Winhance.Infrastructure.Features.Common.Services.InternetConnectivityService(
                        provider.GetRequiredService<ILogService>()
                    )
                );

                // Register the AppInstallationCoordinatorService
                services.AddSingleton<IAppInstallationCoordinatorService>(
                    provider => new Winhance.Infrastructure.Features.SoftwareApps.Services.AppInstallationCoordinatorService(
                        provider.GetRequiredService<IAppInstallationService>(),
                        provider.GetRequiredService<IInternetConnectivityService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetService<Core.Features.UI.Interfaces.INotificationService>(),
                        provider.GetService<Core.Features.Common.Interfaces.IDialogService>()
                    )
                );

                // Use fully qualified type name to resolve ambiguity between Winhance.Core.Models.WindowsService
                // and Winhance.Infrastructure.Features.Common.Services.WindowsSystemService
                services.AddSingleton<ISystemServices>(
                    provider => new Winhance.Infrastructure.Features.Common.Services.WindowsSystemService(
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<IInternetConnectivityService>(),
                        null, // Intentionally not passing IThemeService to break circular dependency
                        provider.GetRequiredService<IUacSettingsService>()
                    )
                );

                // Register theme and wallpaper services
                services.AddSingleton<IWallpaperService, WallpaperService>();
                services.AddSingleton<IThemeService>(provider => new ThemeService(
                    provider.GetRequiredService<IRegistryService>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IWallpaperService>(),
                    provider.GetRequiredService<ISystemServices>()
                ));

                services.AddSingleton<
                    Core.Features.Common.Interfaces.IDialogService,
                    Features.Common.Services.DialogService
                >();
                services.AddSingleton<Features.SoftwareApps.Services.SoftwareAppsDialogService>();
                services.AddSingleton<ITaskProgressService, TaskProgressService>();

                // Register the notification service
                services.AddSingleton<
                    Core.Features.UI.Interfaces.INotificationService,
                    Winhance.Infrastructure.Features.UI.Services.NotificationService
                >();

                // Register the messenger service
                services.AddSingleton<
                    Core.Features.Common.Interfaces.IMessengerService,
                    Features.Common.Services.MessengerService
                >();

                // Register navigation service
                services.AddSingleton<Core.Features.Common.Interfaces.INavigationService>(
                    provider =>
                    {
                        var navigationService = new FrameNavigationService(
                            provider,
                            provider.GetRequiredService<IParameterSerializer>()
                        );

                        // Register view mappings
                        navigationService.RegisterViewMapping(
                            "SoftwareApps",
                            typeof(Features.SoftwareApps.Views.SoftwareAppsView),
                            typeof(Features.SoftwareApps.ViewModels.SoftwareAppsViewModel)
                        );

                        navigationService.RegisterViewMapping(
                            "WindowsApps",
                            typeof(Features.SoftwareApps.Views.WindowsAppsView),
                            typeof(Features.SoftwareApps.ViewModels.WindowsAppsViewModel)
                        );

                        navigationService.RegisterViewMapping(
                            "ExternalApps",
                            typeof(Features.SoftwareApps.Views.ExternalAppsView),
                            typeof(Features.SoftwareApps.ViewModels.ExternalAppsViewModel)
                        );

                        navigationService.RegisterViewMapping(
                            "Optimize",
                            typeof(Features.Optimize.Views.OptimizeView),
                            typeof(Features.Optimize.ViewModels.OptimizeViewModel)
                        );

                        navigationService.RegisterViewMapping(
                            "Customize",
                            typeof(Features.Customize.Views.CustomizeView),
                            typeof(Features.Customize.ViewModels.CustomizeViewModel)
                        );

                        return navigationService;
                    }
                );

                services.AddSingleton<
                    IParameterSerializer,
                    Winhance.Infrastructure.Features.Common.Services.JsonParameterSerializer
                >();

                // Register ThemeManager with navigation service dependency
                services.AddSingleton<IThemeManager>(
                    provider => new Features.Common.Resources.Theme.ThemeManager(
                        provider.GetRequiredService<Core.Features.Common.Interfaces.INavigationService>()
                    )
                );

                // Register design-time data service
                services.AddSingleton<
                    Features.Common.Services.IDesignTimeDataService,
                    Features.Common.Services.DesignTimeDataService
                >();

                // Register window initialization service
                services.AddSingleton<Features.Common.Services.WindowInitializationService>();

                // Register ViewModels with explicit factory methods for all view models
                services.AddSingleton<MainViewModel>(provider => new MainViewModel(
                    provider.GetRequiredService<IThemeManager>(),
                    provider.GetRequiredService<Core.Features.Common.Interfaces.INavigationService>(),
                    provider.GetRequiredService<ITaskProgressService>(),
                    provider.GetRequiredService<Core.Features.Common.Interfaces.IMessengerService>(),
                    provider.GetRequiredService<Core.Features.Common.Interfaces.IDialogService>(),
                    provider.GetRequiredService<IUnifiedConfigurationService>(),
                    provider.GetRequiredService<Features.Common.Services.UserPreferencesService>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<IVersionService>(),
                    provider.GetRequiredService<IApplicationCloseService>(),
                    provider.GetRequiredService<IScriptPathService>()
                ));

                services.AddSingleton<WindowsAppsViewModel>(provider => new WindowsAppsViewModel(
                    provider.GetRequiredService<ITaskProgressService>(),
                    provider.GetRequiredService<ISearchService>(),
                    provider.GetRequiredService<IPackageManager>(),
                    provider.GetRequiredService<IAppInstallationService>(),
                    provider.GetRequiredService<ICapabilityInstallationService>(),
                    provider.GetRequiredService<IFeatureInstallationService>(),
                    provider.GetRequiredService<IFeatureRemovalService>(),
                    provider.GetRequiredService<IConfigurationService>(),
                    provider.GetRequiredService<IScriptDetectionService>(),
                    provider.GetRequiredService<IInternetConnectivityService>(),
                    provider.GetRequiredService<IAppInstallationCoordinatorService>(),
                    provider.GetRequiredService<IBloatRemovalCoordinatorService>(),
                    provider.GetRequiredService<Features.SoftwareApps.Services.SoftwareAppsDialogService>(),
                    provider
                ));
                
                services.AddSingleton<ExternalAppsViewModel>(provider => new ExternalAppsViewModel(
                    provider.GetRequiredService<ITaskProgressService>(),
                    provider.GetRequiredService<ISearchService>(),
                    provider.GetRequiredService<IPackageManager>(),
                    provider.GetRequiredService<IAppInstallationService>(),
                    provider.GetRequiredService<IAppService>(),
                    provider.GetRequiredService<IConfigurationService>(),
                    provider.GetRequiredService<Features.SoftwareApps.Services.SoftwareAppsDialogService>(),
                    provider.GetRequiredService<IInternetConnectivityService>(),
                    provider.GetRequiredService<IAppInstallationCoordinatorService>(),
                    provider
                ));

                // Register UacSettingsService
                services.AddSingleton<IUacSettingsService, UacSettingsService>(
                    provider => new UacSettingsService(
                        provider.GetRequiredService<UserPreferencesService>(),
                        provider.GetRequiredService<ILogService>()
                    )
                );

                // Register child ViewModels for OptimizeViewModel
                services.AddSingleton<WindowsSecurityOptimizationsViewModel>(
                    provider => new WindowsSecurityOptimizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<ISystemServices>(),
                        provider.GetRequiredService<IUacSettingsService>()
                    )
                );

                services.AddSingleton<PrivacyOptimizationsViewModel>(
                    provider => new PrivacyOptimizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<IDependencyManager>(),
                        provider.GetRequiredService<ISystemServices>(),
                        provider.GetRequiredService<IViewModelLocator>(),
                        provider.GetRequiredService<ISettingsRegistry>()
                    )
                );

                services.AddSingleton<GamingandPerformanceOptimizationsViewModel>(
                    provider => new GamingandPerformanceOptimizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<ICommandService>(),
                        provider.GetRequiredService<IViewModelLocator>(),
                        provider.GetRequiredService<ISettingsRegistry>()
                    )
                );

                services.AddSingleton<UpdateOptimizationsViewModel>(
                    provider => new UpdateOptimizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<ISystemServices>(),
                        provider.GetRequiredService<IViewModelLocator>(),
                        provider.GetRequiredService<ISettingsRegistry>()
                    )
                );

                services.AddSingleton<PowerOptimizationsViewModel>(
                    provider => new PowerOptimizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<Winhance.Core.Features.Optimize.Interfaces.IPowerPlanService>(),
                        provider.GetService<IPowerSettingService>(),
                        provider.GetService<IPowerPlanManagerService>(),
                        provider.GetService<IViewModelLocator>(),
                        provider.GetService<ISettingsRegistry>()
                    )
                );

                // ViewModels are registered above

                services.AddSingleton<OptimizeViewModel>(provider => new OptimizeViewModel(
                    provider.GetRequiredService<IRegistryService>(),
                    provider.GetRequiredService<IDialogService>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<ITaskProgressService>(),
                    provider.GetRequiredService<ISearchService>(),
                    provider.GetRequiredService<IConfigurationService>(),
                    provider.GetRequiredService<GamingandPerformanceOptimizationsViewModel>(),
                    provider.GetRequiredService<PrivacyOptimizationsViewModel>(),
                    provider.GetRequiredService<UpdateOptimizationsViewModel>(),
                    provider.GetRequiredService<PowerOptimizationsViewModel>(),
                    provider.GetRequiredService<WindowsSecurityOptimizationsViewModel>(),
                    provider.GetRequiredService<ExplorerOptimizationsViewModel>(),
                    provider.GetRequiredService<NotificationOptimizationsViewModel>(),
                    provider.GetRequiredService<SoundOptimizationsViewModel>(),
                    provider.GetRequiredService<Core.Features.Common.Interfaces.IMessengerService>()
                ));

                // Register child ViewModels for CustomizeViewModel
                services.AddSingleton<TaskbarCustomizationsViewModel>(
                    provider => new TaskbarCustomizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<ISystemServices>()
                    )
                );

                services.AddSingleton<StartMenuCustomizationsViewModel>(
                    provider => new StartMenuCustomizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<ISystemServices>(),
                        provider.GetRequiredService<Core.Features.Common.Interfaces.IDialogService>(),
                        provider.GetRequiredService<IScheduledTaskService>()
                    )
                );

                services.AddSingleton<ExplorerCustomizationsViewModel>(
                    provider => new ExplorerCustomizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<Core.Features.Common.Interfaces.IDialogService>()
                    )
                );

                services.AddSingleton<ExplorerOptimizationsViewModel>(
                    provider => new ExplorerOptimizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<IDependencyManager>(),
                        provider.GetRequiredService<ISystemServices>()
                    )
                );

                services.AddSingleton<NotificationOptimizationsViewModel>(
                    provider => new NotificationOptimizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<Core.Features.Common.Interfaces.IDialogService>(),
                        provider.GetRequiredService<IDependencyManager>(),
                        provider.GetRequiredService<ISystemServices>()
                    )
                );

                services.AddSingleton<SoundOptimizationsViewModel>(
                    provider => new SoundOptimizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<IDependencyManager>(),
                        provider.GetRequiredService<ISystemServices>()
                    )
                );

                services.AddSingleton<WindowsThemeCustomizationsViewModel>(
                    provider => new WindowsThemeCustomizationsViewModel(
                        provider.GetRequiredService<ITaskProgressService>(),
                        provider.GetRequiredService<IRegistryService>(),
                        provider.GetRequiredService<ILogService>(),
                        provider.GetRequiredService<Core.Features.Common.Interfaces.IDialogService>(),
                        provider.GetRequiredService<Core.Features.Customize.Interfaces.IThemeService>()
                    )
                );

                services.AddSingleton<CustomizeViewModel>(provider => new CustomizeViewModel(
                    provider.GetRequiredService<ITaskProgressService>(),
                    provider.GetRequiredService<ISystemServices>(),
                    provider.GetRequiredService<ILogService>(),
                    provider.GetRequiredService<Core.Features.Common.Interfaces.IDialogService>(),
                    provider.GetRequiredService<IThemeManager>(),
                    provider.GetRequiredService<ISearchService>(),
                    provider.GetRequiredService<IConfigurationService>(),
                    provider.GetRequiredService<TaskbarCustomizationsViewModel>(),
                    provider.GetRequiredService<StartMenuCustomizationsViewModel>(),
                    provider.GetRequiredService<ExplorerCustomizationsViewModel>(),
                    provider.GetRequiredService<WindowsThemeCustomizationsViewModel>(),
                    provider.GetRequiredService<Core.Features.Common.Interfaces.IMessengerService>()
                ));

                services.AddSingleton<SoftwareAppsViewModel>(provider => new SoftwareAppsViewModel(
                    provider.GetRequiredService<ITaskProgressService>(),
                    provider.GetRequiredService<IPackageManager>(),
                    provider
                ));

                // Register Views
                services.AddTransient<MainWindow>();

                services.AddTransient<WindowsAppsView>();
                services.AddTransient<ExternalAppsView>();
                services.AddTransient<SoftwareAppsView>();
                services.AddTransient<Features.Optimize.Views.OptimizeView>();
                services.AddTransient<Features.Optimize.Views.PrivacyOptimizationsView>();
                services.AddTransient<Features.Optimize.Views.GamingandPerformanceOptimizationsView>();
                services.AddTransient<Features.Optimize.Views.UpdateOptimizationsView>();
                services.AddTransient<Features.Optimize.Views.PowerOptimizationsView>();
                services.AddTransient<Features.Customize.Views.CustomizeView>();
                services.AddTransient<Features.Customize.Views.TaskbarCustomizationsView>();
                services.AddTransient<Features.Customize.Views.StartMenuCustomizationsView>();
                services.AddTransient<Features.Customize.Views.ExplorerCustomizationsView>();
                services.AddTransient<Features.Optimize.Views.NotificationOptimizationsView>();
                services.AddTransient<Features.Optimize.Views.SoundOptimizationsView>();
                services.AddTransient<Features.Common.Views.DonationDialog>();
                services.AddTransient<LoadingWindow>();

                // Register version service
                services.AddSingleton<IVersionService, VersionService>();
                services.AddSingleton<UpdateNotificationViewModel>();

                // Register ApplicationCloseService
                services.AddSingleton<IApplicationCloseService, ApplicationCloseService>();

                // Register logging service
                services.AddHostedService<LoggingService>();

                LogStartupError("Service configuration complete");
            }
            catch (Exception ex)
            {
                LogStartupError("Error during service configuration", ex);
                throw;
            }
        }
    }
}
