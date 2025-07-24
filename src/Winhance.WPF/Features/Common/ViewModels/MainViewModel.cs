using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Messaging;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Controls;
using Winhance.WPF.Features.Common.Utilities;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        // Window control is now handled through messaging

        private readonly IThemeManager _themeManager;
        private readonly INavigationService _navigationService;
        private readonly IMessengerService _messengerService;
        private readonly IUnifiedConfigurationService _unifiedConfigService;
        private readonly IDialogService _dialogService;
        private readonly Features.Common.Services.UserPreferencesService _userPreferencesService;
        private readonly IApplicationCloseService _applicationCloseService;
        private readonly IVersionService _versionService;
        private readonly ILogService _logService;



        public INavigationService NavigationService => _navigationService;

        [ObservableProperty]
        private object _currentViewModel;

        private string _currentViewName = string.Empty;
        public string CurrentViewName
        {
            get => _currentViewName;
            set => SetProperty(ref _currentViewName, value);
        }

        [ObservableProperty]
        private string _selectedNavigationItem = string.Empty;

        [ObservableProperty]
        private string _maximizeButtonContent = "\uE739";

        /// <summary>
        /// Gets the ViewModel for the More menu functionality
        /// </summary>
        public MoreMenuViewModel MoreMenuViewModel { get; }

        /// <summary>
        /// Gets the command to save a unified configuration.
        /// </summary>
        public ICommand SaveUnifiedConfigCommand { get; }

        /// <summary>
        /// Gets the command to import a unified configuration.
        /// </summary>
        public ICommand ImportUnifiedConfigCommand { get; }

        /// <summary>
        /// Gets the command to open the donation page in a browser.
        /// </summary>
        public ICommand OpenDonateCommand { get; }

        /// <summary>
        /// Gets the command to close the application.
        /// </summary>
        public ICommand CloseCommand { get; }

        /// <summary>
        /// Gets the command to handle the More button click.
        /// </summary>
        public ICommand MoreCommand { get; }



        public MainViewModel(
            IThemeManager themeManager,
            INavigationService navigationService,
            ITaskProgressService progressService,
            IMessengerService messengerService,
            IDialogService dialogService,
            IUnifiedConfigurationService unifiedConfigService,
            Features.Common.Services.UserPreferencesService userPreferencesService,
            ILogService logService,
            IVersionService versionService,
            IApplicationCloseService applicationCloseService,
            IScriptPathService scriptPathService
        )
            : base(progressService, messengerService)
        {
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _navigationService =
                navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _messengerService =
                messengerService ?? throw new ArgumentNullException(nameof(messengerService));
            _dialogService =
                dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _unifiedConfigService =
                unifiedConfigService
                ?? throw new ArgumentNullException(nameof(unifiedConfigService));
            _userPreferencesService =
                userPreferencesService
                ?? throw new ArgumentNullException(nameof(userPreferencesService));
            _applicationCloseService =
                applicationCloseService
                ?? throw new ArgumentNullException(nameof(applicationCloseService));
            _versionService =
                versionService
                ?? throw new ArgumentNullException(nameof(versionService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Initialize the MoreMenuViewModel
            MoreMenuViewModel = new MoreMenuViewModel(
                logService,
                versionService,
                _messengerService,
                applicationCloseService,
                _dialogService,
                scriptPathService
            );

            // Initialize command properties
            SaveUnifiedConfigCommand = new RelayCommand(SaveUnifiedConfig);
            ImportUnifiedConfigCommand = new RelayCommand(ImportUnifiedConfig);
            OpenDonateCommand = new RelayCommand(OpenDonate);
            CloseCommand = new AsyncRelayCommand(CloseWindowAsync);
            MoreCommand = new RelayCommand(HandleMoreButtonClick);

            // Note: View mappings are now registered in App.xaml.cs when configuring the FrameNavigationService

            // Subscribe to navigation events
            _navigationService.Navigated += NavigationService_Navigated;

            // Initialize the application by navigating to default view
            InitializeApplication();

            // We'll initialize with the default view later, after the window is loaded
            // This will be called from MainWindow.xaml.cs after the window is loaded
            // This will be called from MainWindow.xaml.cs after the window is loaded
        }

        private void NavigationService_Navigated(object sender, NavigationEventArgs e)
        {
            CurrentViewName = e.Route;

            // Update the selected navigation item
            SelectedNavigationItem = e.Route;

            // The NavigationService sets e.Parameter to the ViewModel instance
            // This ensures the CurrentViewModel is properly set
            if (e.Parameter != null && e.Parameter is IViewModel)
            {
                CurrentViewModel = e.Parameter;
            }
            else if (e.ViewModelType != null)
            {
                // If for some reason the parameter is not set, try to get the ViewModel from the service provider
                try
                {
                    // Get the view model from the navigation event parameter
                    if (e.Parameter != null)
                    {
                        CurrentViewModel = e.Parameter;
                    }
                }
                catch (Exception ex)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = $"Error getting current view model: {ex.Message}",
                            Level = LogLevel.Error,
                            Exception = ex,
                        }
                    );
                }
            }
        }

        [RelayCommand]
        private void Navigate(string viewName)
        {
            try
            {
                _navigationService.NavigateTo(viewName);
            }
            catch (Exception ex)
            {
                // Log the error using the messaging system
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = $"Navigation error to {viewName}: {ex.Message}",
                        Level = LogLevel.Error,
                        Exception = ex,
                    }
                );

                MessageBox.Show(
                    $"Navigation error while attempting to navigate to {viewName}.\nError: {ex.Message}",
                    "Navigation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            _themeManager.ToggleTheme();
        }



        [RelayCommand]
        private void MinimizeWindow()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null)
                {
                    _logService?.LogWarning("Cannot minimize window: MainWindow is null");
                    return;
                }

                // Try to minimize the window directly
                mainWindow.WindowState = WindowState.Minimized;
                _logService?.LogInformation("Window minimized successfully");
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Failed to minimize window directly: {ex.Message}", ex);
                
                // Fall back to messaging
                try
                {
                    _messengerService.Send(
                        new WindowStateMessage
                        {
                            Action = WindowStateMessage.WindowStateAction.Minimize,
                        }
                    );
                    _logService?.LogInformation("Sent minimize window message as fallback");
                }
                catch (Exception msgEx)
                {
                    _logService?.LogError($"Failed to send minimize window message: {msgEx.Message}", msgEx);
                }
            }
        }



        [RelayCommand]
        private void MaximizeRestoreWindow()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null)
                {
                    _logService?.LogWarning("Cannot maximize/restore window: MainWindow is null");
                    return;
                }

                if (mainWindow.WindowState == WindowState.Maximized)
                {
                    // Try to restore the window directly
                    try
                    {
                        mainWindow.WindowState = WindowState.Normal;
                        MaximizeButtonContent = "\uE739"; // Maximize icon
                        _logService?.LogInformation("Window restored successfully");
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogError($"Failed to restore window directly: {ex.Message}", ex);
                        
                        // Fall back to messaging
                        try
                        {
                            _messengerService.Send(
                                new WindowStateMessage
                                {
                                    Action = WindowStateMessage.WindowStateAction.Restore,
                                }
                            );
                            MaximizeButtonContent = "\uE739"; // Maximize icon
                            _logService?.LogInformation("Sent restore window message as fallback");
                        }
                        catch (Exception msgEx)
                        {
                            _logService?.LogError($"Failed to send restore window message: {msgEx.Message}", msgEx);
                        }
                    }
                }
                else
                {
                    // Try to maximize the window directly
                    try
                    {
                        mainWindow.WindowState = WindowState.Maximized;
                        MaximizeButtonContent = "\uE923"; // Restore icon
                        _logService?.LogInformation("Window maximized successfully");
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogError($"Failed to maximize window directly: {ex.Message}", ex);
                        
                        // Fall back to messaging
                        try
                        {
                            _messengerService.Send(
                                new WindowStateMessage
                                {
                                    Action = WindowStateMessage.WindowStateAction.Maximize,
                                }
                            );
                            MaximizeButtonContent = "\uE923"; // Restore icon
                            _logService?.LogInformation("Sent maximize window message as fallback");
                        }
                        catch (Exception msgEx)
                        {
                            _logService?.LogError($"Failed to send maximize window message: {msgEx.Message}", msgEx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Unexpected error in MaximizeRestoreWindow: {ex.Message}", ex);
            }
        }



        // Make sure this method is public so it can be called directly for testing
        [RelayCommand]
        public async Task CloseWindowAsync()
        {
            try
            {
                _logService?.LogInformation("Close window command executed");
                
                // Use the ApplicationCloseService to handle the close process with donation dialog
                await _applicationCloseService.CloseApplicationWithSupportDialogAsync();
                _logService?.LogInformation("Application close service completed");
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in CloseWindowAsync: {ex.Message}", ex);
                
                // Fall back to direct window closing
                try
                {
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.Close();
                        _logService?.LogInformation("Fallback: Window closed directly");
                    }
                    else
                    {
                        // Fall back to messaging
                        _messengerService.Send(
                            new WindowStateMessage { Action = WindowStateMessage.WindowStateAction.Close }
                        );
                        _logService?.LogInformation("Fallback: Sent close window message");
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logService?.LogError($"Failed in fallback close: {fallbackEx.Message}", fallbackEx);
                    
                    // Last resort - force application shutdown
                    try
                    {
                        Application.Current.Shutdown();
                    }
                    catch (Exception shutdownEx)
                    {
                        _logService?.LogError($"Failed to shutdown application: {shutdownEx.Message}", shutdownEx);
                    }
                }
            }
        }

        /// <summary>
        /// Saves a unified configuration by delegating to the current view model.
        /// </summary>
        private async void SaveUnifiedConfig()
        {
            try
            {
                // Use the injected unified configuration service
                var unifiedConfigService = _unifiedConfigService;

                if (unifiedConfigService == null)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "UnifiedConfigurationService not available",
                            Level = LogLevel.Error,
                        }
                    );
                    return;
                }

                _messengerService.Send(
                    new LogMessage
                    {
                        Message = "Using UnifiedConfigurationService to save unified configuration",
                        Level = LogLevel.Info,
                    }
                );

                // Create a unified configuration with settings from all view models
                var unifiedConfig = await unifiedConfigService.CreateUnifiedConfigurationAsync();

                // Get the configuration service from the application
                IConfigurationService configService = null;

                // Try to get the service from the application
                if (Application.Current is App appInstance3)
                {
                    try
                    {
                        // Use reflection to access the _host.Services property
                        var hostField = appInstance3
                            .GetType()
                            .GetField("_host", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (hostField != null)
                        {
                            var host = hostField.GetValue(appInstance3);
                            var servicesProperty = host.GetType().GetProperty("Services");
                            if (servicesProperty != null)
                            {
                                var services = servicesProperty.GetValue(host);
                                var getServiceMethod = services
                                    .GetType()
                                    .GetMethod("GetService", new[] { typeof(Type) });
                                if (getServiceMethod != null)
                                {
                                    configService =
                                        getServiceMethod.Invoke(
                                            services,
                                            new object[] { typeof(IConfigurationService) }
                                        ) as IConfigurationService;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _messengerService.Send(
                            new LogMessage
                            {
                                Message = $"Error accessing ConfigurationService: {ex.Message}",
                                Level = LogLevel.Error,
                                Exception = ex,
                            }
                        );
                    }
                }

                if (configService == null)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "ConfigurationService not available",
                            Level = LogLevel.Error,
                        }
                    );
                    return;
                }

                // Save the unified configuration
                bool saveResult = await unifiedConfigService.SaveUnifiedConfigurationAsync(
                    unifiedConfig
                );

                if (saveResult)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "Unified configuration saved successfully",
                            Level = LogLevel.Info,
                        }
                    );

                    // Show a single success dialog using CustomDialog to match the application style
                    var sections = new List<string>();
                    if (unifiedConfig.WindowsApps.Items.Any())
                        sections.Add("Windows Apps");
                    if (unifiedConfig.ExternalApps.Items.Any())
                        sections.Add("External Apps");
                    if (unifiedConfig.Customize.Items.Any())
                        sections.Add("Customizations");
                    if (unifiedConfig.Optimize.Items.Any())
                        sections.Add("Optimizations");

                    Winhance.WPF.Features.Common.Views.CustomDialog.ShowInformation(
                        "Configuration Saved",
                        "Configuration saved successfully.",
                        sections,
                        "You can now import this configuration on another system."
                    );
                }
                else
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "Save unified configuration canceled by user",
                            Level = LogLevel.Info,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = $"Error saving unified configuration: {ex.Message}",
                        Level = LogLevel.Error,
                        Exception = ex,
                    }
                );
            }
        }

        // FallbackToViewModelImplementation method removed as part of unified configuration cleanup

        /// <summary>
        /// Imports a unified configuration by delegating to the current view model.
        /// </summary>
        private async void ImportUnifiedConfig()
        {
            try
            {
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = "Starting unified configuration import process",
                        Level = LogLevel.Info,
                    }
                );

                // Use the injected unified configuration service
                var unifiedConfigService = _unifiedConfigService;

                _messengerService.Send(
                    new LogMessage
                    {
                        Message =
                            "Using UnifiedConfigurationService to import unified configuration",
                        Level = LogLevel.Info,
                    }
                );

                // Get the configuration service from the application
                IConfigurationService configService = null;

                // Try to get the service from the application
                if (Application.Current is App appInstance2)
                {
                    try
                    {
                        // Use reflection to access the _host.Services property
                        var hostField = appInstance2
                            .GetType()
                            .GetField("_host", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (hostField != null)
                        {
                            var host = hostField.GetValue(appInstance2);
                            var servicesProperty = host.GetType().GetProperty("Services");
                            if (servicesProperty != null)
                            {
                                var services = servicesProperty.GetValue(host);
                                var getServiceMethod = services
                                    .GetType()
                                    .GetMethod("GetService", new[] { typeof(Type) });
                                if (getServiceMethod != null)
                                {
                                    configService =
                                        getServiceMethod.Invoke(
                                            services,
                                            new object[] { typeof(IConfigurationService) }
                                        ) as IConfigurationService;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _messengerService.Send(
                            new LogMessage
                            {
                                Message = $"Error accessing ConfigurationService: {ex.Message}",
                                Level = LogLevel.Error,
                                Exception = ex,
                            }
                        );
                    }
                }

                if (configService == null)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "ConfigurationService not available",
                            Level = LogLevel.Error,
                        }
                    );
                    return;
                }

                // Show the config import options dialog using the DialogService
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = "Showing config import options dialog",
                        Level = LogLevel.Info,
                    }
                );

                // Use the DialogService to show the config import options dialog
                var selectedOption = await _dialogService.ShowConfigImportOptionsDialogAsync();

                if (selectedOption == null)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "User canceled config import options dialog",
                            Level = LogLevel.Info,
                        }
                    );
                    return;
                }

                UnifiedConfigurationFile unifiedConfig = null;

                // Process the selected option
                switch (selectedOption)
                {
                    case ImportOption.ImportOwn:
                        _messengerService.Send(
                            new LogMessage
                            {
                                Message = "User selected to import their own configuration",
                                Level = LogLevel.Info,
                            }
                        );

                        // Load the unified configuration from file
                        _messengerService.Send(
                            new LogMessage
                            {
                                Message = "Showing file dialog to select configuration file",
                                Level = LogLevel.Info,
                            }
                        );

                        unifiedConfig = await unifiedConfigService.LoadUnifiedConfigurationAsync();
                        break;

                    case ImportOption.ImportRecommended:
                        _messengerService.Send(
                            new LogMessage
                            {
                                Message = "User selected to import recommended configuration",
                                Level = LogLevel.Info,
                            }
                        );

                        // Download and load the recommended configuration
                        _messengerService.Send(
                            new LogMessage
                            {
                                Message = "Downloading recommended configuration",
                                Level = LogLevel.Info,
                            }
                        );

                        unifiedConfig = await configService.LoadRecommendedConfigurationAsync();
                        break;
                }

                if (unifiedConfig == null)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "Import unified configuration canceled by user",
                            Level = LogLevel.Info,
                        }
                    );
                    return;
                }

                _messengerService.Send(
                    new LogMessage
                    {
                        Message =
                            $"Configuration loaded with sections: WindowsApps ({unifiedConfig.WindowsApps.Items.Count} items), "
                            + $"ExternalApps ({unifiedConfig.ExternalApps.Items.Count} items), "
                            + $"Customize ({unifiedConfig.Customize.Items.Count} items), "
                            + $"Optimize ({unifiedConfig.Optimize.Items.Count} items)",
                        Level = LogLevel.Info,
                    }
                );

                // Show the unified configuration dialog to let the user select which sections to import
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = "Showing unified configuration dialog for section selection",
                        Level = LogLevel.Info,
                    }
                );

                // Create a dictionary of sections with their availability and item counts
                var sectionInfo = new Dictionary<
                    string,
                    (bool IsSelected, bool IsAvailable, int ItemCount)
                >
                {
                    // Add Software & Apps parent section
                    {
                        "Software & Apps",
                        (
                            true,
                            unifiedConfig.WindowsApps.Items.Count > 0 || unifiedConfig.ExternalApps.Items.Count > 0,
                            unifiedConfig.WindowsApps.Items.Count + unifiedConfig.ExternalApps.Items.Count
                        )
                    },
                    // Add Windows Apps and External Apps as subsections
                    {
                        "WindowsApps",
                        (
                            true,
                            unifiedConfig.WindowsApps.Items.Count > 0,
                            unifiedConfig.WindowsApps.Items.Count
                        )
                    },
                    {
                        "ExternalApps",
                        (
                            true,
                            unifiedConfig.ExternalApps.Items.Count > 0,
                            unifiedConfig.ExternalApps.Items.Count
                        )
                    },
                    // Optimization Settings with subsections
                    {
                        "Optimize",
                        (
                            true,
                            unifiedConfig.Optimize.Items.Count > 0,
                            unifiedConfig.Optimize.Items.Count
                        )
                    },
                    // Optimization subsections
                    { "Optimize.GamingAndPerformance", (true, true, 0) },
                    { "Optimize.PowerSettings", (true, true, 0) },
                    { "Optimize.WindowsSecuritySettings", (true, true, 0) },
                    { "Optimize.PrivacySettings", (true, true, 0) },
                    { "Optimize.WindowsUpdates", (true, true, 0) },
                    { "Optimize.Explorer", (true, true, 0) },
                    { "Optimize.Notifications", (true, true, 0) },
                    { "Optimize.Sound", (true, true, 0) },
                    
                    // Customization Settings with subsections
                    {
                        "Customize",
                        (
                            true,
                            unifiedConfig.Customize.Items.Count > 0,
                            unifiedConfig.Customize.Items.Count
                        )
                    },
                    // Customization subsections
                    { "Customize.WindowsTheme", (true, true, 0) },
                    { "Customize.Taskbar", (true, true, 0) },
                    { "Customize.StartMenu", (true, true, 0) },
                    { "Customize.Explorer", (true, true, 0) },
                };

                // Use the DialogService to show the unified configuration import dialog
                var result = await _dialogService.ShowUnifiedConfigurationImportDialogAsync(
                    "Select Configuration Sections",
                    "Select which sections you want to import from the unified configuration.",
                    sectionInfo
                );

                if (result == null)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "User canceled unified configuration import",
                            Level = LogLevel.Info,
                        }
                    );
                    return;
                }

                // Get the selected sections from the dialog result
                var selectedSections = result
                    .Where(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToList();

                _messengerService.Send(
                    new LogMessage
                    {
                        Message = $"Selected sections: {string.Join(", ", selectedSections)}",
                        Level = LogLevel.Info,
                    }
                );

                if (!selectedSections.Any())
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = "No sections selected for import",
                            Level = LogLevel.Info,
                        }
                    );
                    _dialogService.ShowMessage(
                        "Please select at least one section to import from the unified configuration.",
                        "No sections selected"
                    );
                    return;
                }

                // Apply the configuration to the selected sections
                _messengerService.Send(
                    new LogMessage
                    {
                        Message =
                            $"Applying configuration to selected sections: {string.Join(", ", selectedSections)}",
                        Level = LogLevel.Info,
                    }
                );

                // Apply the configuration to the selected sections
                await unifiedConfigService.ApplyUnifiedConfigurationAsync(
                    unifiedConfig,
                    selectedSections
                );

                // Always show a success message since the settings are being applied correctly
                // even if the updatedCount is 0
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = "Unified configuration imported successfully",
                        Level = LogLevel.Info,
                    }
                );

                // Show a success message using CustomDialog to match the application style
                var importedSections = new List<string>();
                foreach (var section in selectedSections)
                {
                    switch (section)
                    {
                        case "WindowsApps":
                            importedSections.Add("Windows Apps");
                            break;
                        case "ExternalApps":
                            importedSections.Add("External Apps");
                            break;
                        case "Customize":
                            importedSections.Add("Customizations");
                            break;
                        case "Optimize":
                            importedSections.Add("Optimizations");
                            break;
                    }
                }

                Winhance.WPF.Features.Common.Views.CustomDialog.ShowInformation(
                    "Configuration Imported",
                    "The unified configuration has been imported successfully.",
                    importedSections,
                    "The selected settings have been applied to your system."
                );
            }
            catch (Exception ex)
            {
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = $"Error importing unified configuration: {ex.Message}",
                        Level = LogLevel.Error,
                        Exception = ex,
                    }
                );

                // Show an error message to the user
                if (_dialogService != null)
                {
                    _dialogService.ShowMessage(
                        $"An error occurred while importing the configuration: {ex.Message}",
                        "Import Error"
                    );
                }
            }
        }

        // FallbackToViewModelImportImplementation method removed as part of unified configuration cleanup

        /// <summary>
        /// Opens the donation page in the default browser.
        /// </summary>
        private void OpenDonate()
        {
            try
            {
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = "Opening donation page in browser",
                        Level = LogLevel.Info,
                    }
                );

                // Use ProcessStartInfo to open the URL in the default browser
                var psi = new ProcessStartInfo
                {
                    FileName = "https://ko-fi.com/memstechtips",
                    UseShellExecute = true,
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _messengerService.Send(
                    new LogMessage
                    {
                        Message = $"Error opening donation page: {ex.Message}",
                        Level = LogLevel.Error,
                        Exception = ex,
                    }
                );

                // Show an error message to the user
                if (_dialogService != null)
                {
                    _dialogService.ShowMessage(
                        $"An error occurred while opening the donation page: {ex.Message}",
                        "Error"
                    );
                }
            }
        }

        /// <summary>
        /// Initializes the application by navigating to the default view
        /// </summary>
        private void InitializeApplication()
        {
            try
            {
                _navigationService.NavigateTo("SoftwareApps");
            }
            catch (Exception ex)
            {
                try
                {
                    _navigationService.NavigateTo("About");
                }
                catch (Exception fallbackEx)
                {
                    _messengerService.Send(
                        new LogMessage
                        {
                            Message = $"Failed to navigate to default views: {ex.Message}, Fallback: {fallbackEx.Message}",
                            Level = LogLevel.Error,
                            Exception = ex,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Handles the More button click functionality using flyout overlay
        /// </summary>
        public void HandleMoreButtonClick()
        {
            try
            {
                _logService?.LogInformation("More button clicked - showing flyout");
                
                // Set the selected navigation item to show visual feedback
                SelectedNavigationItem = "More";
                
                ShowMoreMenuFlyout();
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in HandleMoreButtonClick: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Shows the MoreMenu flyout overlay
        /// </summary>
        public void ShowMoreMenuFlyout()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    _logService?.LogInformation("MainWindow found, showing flyout overlay");
                    
                    // Find the overlay and menu elements
                    var overlay = mainWindow.FindName("MoreMenuOverlay") as FrameworkElement;
                    var flyoutContent = mainWindow.FindName("MoreMenuFlyoutContent") as FrameworkElement;
                    var moreButton = mainWindow.FindName("MoreButton") as FrameworkElement;
                    
                    _logService?.LogInformation($"Elements found - Overlay: {overlay != null}, FlyoutContent: {flyoutContent != null}, MoreButton: {moreButton != null}");
                    
                    if (overlay != null && flyoutContent != null && moreButton != null)
                    {
                        // Calculate position relative to the More button
                        var buttonPosition = moreButton.TransformToAncestor(mainWindow).Transform(new Point(0, 0));
                        
                        _logService?.LogInformation($"Button position: X={buttonPosition.X}, Y={buttonPosition.Y}, Button size: {moreButton.ActualWidth}x{moreButton.ActualHeight}");
                        
                        // Position the flyout to the right of the More button, positioned higher for full visibility
                        var flyoutMargin = new Thickness(
                            buttonPosition.X + moreButton.ActualWidth + 5, // 5px spacing to the right
                            buttonPosition.Y - (moreButton.ActualHeight * 2) - 45, // Position well above the button for full menu visibility
                            0,
                            0
                        );
                        
                        _logService?.LogInformation($"Setting flyout margin: Left={flyoutMargin.Left}, Top={flyoutMargin.Top}");
                        
                        flyoutContent.Margin = flyoutMargin;
                        overlay.Visibility = Visibility.Visible;
                        
                        _logService?.LogInformation($"Overlay visibility set to: {overlay.Visibility}");
                        
                        // Set focus on the overlay to enable keyboard events (Escape to close)
                        overlay.Focus();
                        
                        _logService?.LogInformation("MoreMenu flyout shown successfully");
                    }
                    else
                    {
                        _logService?.LogWarning($"Could not find required flyout elements - Overlay: {overlay != null}, FlyoutContent: {flyoutContent != null}, MoreButton: {moreButton != null}");
                    }
                }
                else
                {
                    _logService?.LogWarning("MainWindow is null, cannot show flyout");
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error showing MoreMenu flyout: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Closes the MoreMenu flyout overlay
        /// </summary>
        public void CloseMoreMenuFlyout()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var overlay = mainWindow.FindName("MoreMenuOverlay") as FrameworkElement;
                    if (overlay != null)
                    {
                        overlay.Visibility = Visibility.Collapsed;
                        _logService?.LogInformation("MoreMenu flyout closed");
                        
                        // Reset navigation selection
                        SelectedNavigationItem = CurrentViewName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error closing MoreMenu flyout: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Handles window state changes and updates the maximize button content
        /// </summary>
        /// <param name="windowState">The current window state</param>
        public void HandleWindowStateChanged(WindowState windowState)
        {
            MaximizeButtonContent = windowState == WindowState.Maximized
                ? "WindowRestore"
                : "WindowMaximize";
        }

        /// <summary>
        /// Gets the theme-appropriate icon path
        /// </summary>
        /// <returns>The icon path for the current theme</returns>
        public string GetThemeIconPath()
        {
            return _themeManager.IsDarkTheme
                ? "pack://application:,,,/Resources/AppIcons/winhance-rocket-white-transparent-bg.ico"
                : "pack://application:,,,/Resources/AppIcons/winhance-rocket-black-transparent-bg.ico";
        }

        /// <summary>
        /// Gets the default fallback icon path
        /// </summary>
        /// <returns>The default icon path</returns>
        public string GetDefaultIconPath()
        {
            return "pack://application:,,,/Resources/AppIcons/winhance-rocket.ico";
        }

        /// <summary>
        /// Requests the view to update its theme icon
        /// </summary>
        public void RequestThemeIconUpdate()
        {
            _messengerService.Send(new UpdateThemeIconMessage());
        }

        /// <summary>
        /// Helper method to find visual children in the visual tree
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                {
                    return (T)child;
                }

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}
