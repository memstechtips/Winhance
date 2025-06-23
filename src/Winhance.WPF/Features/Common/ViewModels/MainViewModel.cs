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

        public INavigationService NavigationService => _navigationService;

        [ObservableProperty]
        private object _currentViewModel;

        // Helper method to log window actions for debugging
        private void LogWindowAction(string message)
        {
            string formattedMessage = $"Window Action: {message}";

            // Send to application logging system
            _messengerService.Send(
                new LogMessage { Message = formattedMessage, Level = LogLevel.Debug }
            );

            // Also log to diagnostic file for troubleshooting
            FileLogger.Log("MainViewModel", formattedMessage);
        }

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
            IApplicationCloseService applicationCloseService
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

            // Initialize the MoreMenuViewModel
            MoreMenuViewModel = new MoreMenuViewModel(
                logService,
                versionService,
                _messengerService,
                applicationCloseService,
                _dialogService
            );

            // Initialize command properties
            _minimizeWindowCommand = new RelayCommand(MinimizeWindow);
            _maximizeRestoreWindowCommand = new RelayCommand(MaximizeRestoreWindow);
            // Use AsyncRelayCommand instead of RelayCommand for async methods
            _closeWindowCommand = new AsyncRelayCommand(CloseWindowAsync);
            SaveUnifiedConfigCommand = new RelayCommand(SaveUnifiedConfig);
            ImportUnifiedConfigCommand = new RelayCommand(ImportUnifiedConfig);
            OpenDonateCommand = new RelayCommand(OpenDonate);

            // Note: View mappings are now registered in App.xaml.cs when configuring the FrameNavigationService

            // Subscribe to navigation events
            _navigationService.Navigated += NavigationService_Navigated;

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

        // Explicit command property for MinimizeWindow
        private ICommand _minimizeWindowCommand;
        public ICommand MinimizeWindowCommand
        {
            get
            {
                if (_minimizeWindowCommand == null)
                {
                    _minimizeWindowCommand = new RelayCommand(MinimizeWindow);
                }
                return _minimizeWindowCommand;
            }
        }

        private void MinimizeWindow()
        {
            LogWindowAction("MinimizeWindow command called");

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                LogWindowAction("MainWindow is null");
                return;
            }

            // Try to minimize the window directly
            try
            {
                mainWindow.WindowState = WindowState.Minimized;
                LogWindowAction("Directly set WindowState to Minimized");
            }
            catch (Exception ex)
            {
                LogWindowAction($"Error setting WindowState directly: {ex.Message}");

                // Fall back to messaging
                _messengerService.Send(
                    new WindowStateMessage
                    {
                        Action = WindowStateMessage.WindowStateAction.Minimize,
                    }
                );
            }
        }

        // Explicit command property for MaximizeRestoreWindow
        private ICommand _maximizeRestoreWindowCommand;
        public ICommand MaximizeRestoreWindowCommand
        {
            get
            {
                if (_maximizeRestoreWindowCommand == null)
                {
                    _maximizeRestoreWindowCommand = new RelayCommand(MaximizeRestoreWindow);
                }
                return _maximizeRestoreWindowCommand;
            }
        }

        private void MaximizeRestoreWindow()
        {
            LogWindowAction("MaximizeRestoreWindow command called");

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                LogWindowAction("MainWindow is null");
                return;
            }

            if (mainWindow.WindowState == WindowState.Maximized)
            {
                // Try to restore the window directly
                try
                {
                    mainWindow.WindowState = WindowState.Normal;
                    LogWindowAction("Directly set WindowState to Normal");
                }
                catch (Exception ex)
                {
                    LogWindowAction($"Error setting WindowState directly: {ex.Message}");

                    // Fall back to messaging
                    _messengerService.Send(
                        new WindowStateMessage
                        {
                            Action = WindowStateMessage.WindowStateAction.Restore,
                        }
                    );
                }

                // Update the button icon
                MaximizeButtonContent = "\uE739";
                LogWindowAction("Updated MaximizeButtonContent to Maximize icon");
            }
            else
            {
                // Try to maximize the window directly
                try
                {
                    mainWindow.WindowState = WindowState.Maximized;
                    LogWindowAction("Directly set WindowState to Maximized");
                }
                catch (Exception ex)
                {
                    LogWindowAction($"Error setting WindowState directly: {ex.Message}");

                    // Fall back to messaging
                    _messengerService.Send(
                        new WindowStateMessage
                        {
                            Action = WindowStateMessage.WindowStateAction.Maximize,
                        }
                    );
                }

                // Update the button icon
                MaximizeButtonContent = "\uE923";
                LogWindowAction("Updated MaximizeButtonContent to Restore icon");
            }
        }

        // Explicit command property for CloseWindow
        private ICommand _closeWindowCommand;
        public ICommand CloseWindowCommand
        {
            get
            {
                if (_closeWindowCommand == null)
                {
                    // Use AsyncRelayCommand instead of RelayCommand for async methods
                    _closeWindowCommand = new AsyncRelayCommand(CloseWindowAsync);
                }
                return _closeWindowCommand;
            }
        }

        // Make sure this method is public so it can be called directly for testing
        public async Task CloseWindowAsync()
        {
            LogWindowAction("CloseWindowAsync method called");

            // Log basic information
            _messengerService.Send(
                new LogMessage
                {
                    Message = "CloseWindow method executing in MainViewModel",
                    Level = LogLevel.Debug,
                }
            );

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                LogWindowAction("ERROR: MainWindow is null");
                return;
            }

            // The donation dialog is now handled in MainWindow.CloseButton_Click
            // so we can simply close the window here

            try
            {
                LogWindowAction("About to call mainWindow.Close()");
                mainWindow.Close();
                LogWindowAction("mainWindow.Close() called successfully");
            }
            catch (Exception ex)
            {
                LogWindowAction($"ERROR closing window directly: {ex.Message}");

                // Fall back to messaging
                LogWindowAction("Falling back to WindowStateMessage.Close");
                _messengerService.Send(
                    new WindowStateMessage { Action = WindowStateMessage.WindowStateAction.Close }
                );
            }

            LogWindowAction("CloseWindowAsync method completed");
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
    }
}
