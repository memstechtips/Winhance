using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Messaging;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// ViewModel for the More menu functionality
    /// </summary>
    public class MoreMenuViewModel : ObservableObject
    {
        private readonly ILogService _logService;
        private readonly IVersionService _versionService;
        private readonly IMessengerService _messengerService;
        private readonly IApplicationCloseService _applicationCloseService;
        private readonly IDialogService _dialogService;

        private string _versionInfo;

        /// <summary>
        /// Gets or sets the version information text displayed in the menu
        /// </summary>
        public string VersionInfo
        {
            get => _versionInfo;
            set => SetProperty(ref _versionInfo, value);
        }

        /// <summary>
        /// Command to check for application updates
        /// </summary>
        public ICommand CheckForUpdatesCommand { get; }

        /// <summary>
        /// Command to open the logs folder
        /// </summary>
        public ICommand OpenLogsCommand { get; }

        /// <summary>
        /// Command to open the scripts folder
        /// </summary>
        public ICommand OpenScriptsCommand { get; }

        /// <summary>
        /// Command to close the application
        /// </summary>
        public ICommand CloseApplicationCommand { get; }

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        /// <param name="logService">Service for logging</param>
        /// <param name="versionService">Service for version information and updates</param>
        /// <param name="messengerService">Service for messaging between components</param>
        public MoreMenuViewModel(
            ILogService logService,
            IVersionService versionService,
            IMessengerService messengerService,
            IApplicationCloseService applicationCloseService,
            IDialogService dialogService
        )
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _versionService =
                versionService ?? throw new ArgumentNullException(nameof(versionService));
            _messengerService =
                messengerService ?? throw new ArgumentNullException(nameof(messengerService));
            _applicationCloseService =
                applicationCloseService
                ?? throw new ArgumentNullException(nameof(applicationCloseService));
            _dialogService =
                dialogService
                ?? throw new ArgumentNullException(nameof(dialogService));

            // Initialize version info
            UpdateVersionInfo();

            // Initialize commands with explicit execute and canExecute methods
            CheckForUpdatesCommand = new RelayCommand(
                execute: () =>
                {
                    _logService.LogInformation("CheckForUpdatesCommand executed");
                    CheckForUpdatesAsync();
                },
                canExecute: () => true
            );

            OpenLogsCommand = new RelayCommand(
                execute: () =>
                {
                    _logService.LogInformation("OpenLogsCommand executed");
                    OpenLogs();
                },
                canExecute: () => true
            );

            OpenScriptsCommand = new RelayCommand(
                execute: () =>
                {
                    _logService.LogInformation("OpenScriptsCommand executed");
                    OpenScripts();
                },
                canExecute: () => true
            );

            CloseApplicationCommand = new RelayCommand(
                execute: () =>
                {
                    _logService.LogInformation("CloseApplicationCommand executed");
                    CloseApplication();
                },
                canExecute: () => true
            );
        }

        /// <summary>
        /// Updates the version information text
        /// </summary>
        private void UpdateVersionInfo()
        {
            try
            {
                // Get the current version from the version service
                VersionInfo versionInfo = _versionService.GetCurrentVersion();

                // Format the version text
                VersionInfo = $"Winhance Version {versionInfo.Version}";
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error updating version info: {ex.Message}", ex);

                // Set a default version text in case of error
                VersionInfo = "Winhance Version";
            }
        }

        /// <summary>
        /// Checks for updates and shows appropriate dialog
        /// </summary>
        private async void CheckForUpdatesAsync()
        {
            try
            {
                _logService.LogInformation("Checking for updates from MoreMenu");

                // Get the current version
                VersionInfo currentVersion = _versionService.GetCurrentVersion();
                _logService.LogInformation($"Current version: {currentVersion.Version}");

                // Check for updates
                VersionInfo latestVersion = await _versionService.CheckForUpdateAsync();
                _logService.LogInformation(
                    $"Latest version: {latestVersion.Version}, Update available: {latestVersion.IsUpdateAvailable}"
                );

                if (latestVersion.IsUpdateAvailable)
                {
                    // Show update dialog
                    string title = "Update Available";
                    string message = "Good News! A New Version of Winhance is available.";

                    _logService.LogInformation("Showing update dialog");
                    // Show the update dialog
                    await UpdateDialog.ShowAsync(
                        title,
                        message,
                        currentVersion,
                        latestVersion,
                        async () =>
                        {
                            _logService.LogInformation(
                                "User initiated update download and installation"
                            );
                            await _versionService.DownloadAndInstallUpdateAsync();
                        }
                    );
                }
                else
                {
                    _logService.LogInformation("No updates available");
                    // Show a message that no update is available
                    _dialogService.ShowInformationAsync(
                        "You have the latest version of Winhance.",
                        "No Updates Available"
                    );
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking for updates: {ex.Message}", ex);

                // Show an error message
                _dialogService.ShowErrorAsync(
                    $"An error occurred while checking for updates: {ex.Message}",
                    "Update Check Error"
                );
            }
        }

        /// <summary>
        /// Opens the logs folder
        /// </summary>
        private void OpenLogs()
        {
            try
            {
                // Get the logs folder path
                string logsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Winhance",
                    "Logs"
                );

                // Create the folder if it doesn't exist
                if (!Directory.Exists(logsFolder))
                {
                    Directory.CreateDirectory(logsFolder);
                }

                // Open the logs folder using ProcessStartInfo with UseShellExecute=true
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = logsFolder,
                    UseShellExecute = true,
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error opening logs folder: {ex.Message}", ex);

                // Show an error message
                _dialogService.ShowErrorAsync(
                    $"An error occurred while opening the logs folder: {ex.Message}",
                    "Logs Folder Error"
                );
            }
        }

        /// <summary>
        /// Opens the scripts folder
        /// </summary>
        private void OpenScripts()
        {
            try
            {
                // Get the scripts folder path
                string scriptsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Winhance",
                    "Scripts"
                );

                // Create the folder if it doesn't exist
                if (!Directory.Exists(scriptsFolder))
                {
                    Directory.CreateDirectory(scriptsFolder);
                }

                // Open the scripts folder using ProcessStartInfo with UseShellExecute=true
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = scriptsFolder,
                    UseShellExecute = true,
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error opening scripts folder: {ex.Message}", ex);

                // Show an error message
                _dialogService.ShowErrorAsync(
                    $"An error occurred while opening the scripts folder: {ex.Message}",
                    "Scripts Folder Error"
                );
            }
        }

        /// <summary>
        /// Closes the application using the same behavior as the normal close button
        /// </summary>
        private async void CloseApplication()
        {
            try
            {
                _logService.LogInformation(
                    "Closing application from MoreMenu, delegating to ApplicationCloseService"
                );
                await _applicationCloseService.CloseApplicationWithSupportDialogAsync();
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error closing application: {ex.Message}", ex);

                // Fallback to direct application shutdown if everything else fails
                try
                {
                    _logService.LogInformation("Falling back to Application.Current.Shutdown()");
                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                }
                catch (Exception shutdownEx)
                {
                    _logService.LogError(
                        $"Error shutting down application: {shutdownEx.Message}",
                        shutdownEx
                    );

                    // Last resort
                    Environment.Exit(0);
                }
            }
        }
    }
}
