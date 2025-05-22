using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.UI.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Coordinates the installation of applications, handling connectivity monitoring,
    /// user notifications, and installation state management.
    /// </summary>
    public class AppInstallationCoordinatorService : IAppInstallationCoordinatorService
    {
        private readonly IAppInstallationService _appInstallationService;
        private readonly IInternetConnectivityService _connectivityService;
        private readonly ILogService _logService;
        private readonly INotificationService _notificationService;
        private readonly IDialogService _dialogService;
        
        /// <summary>
        /// Event that is raised when the installation status changes.
        /// </summary>
        public event EventHandler<InstallationStatusChangedEventArgs> InstallationStatusChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppInstallationCoordinatorService"/> class.
        /// </summary>
        /// <param name="appInstallationService">The application installation service.</param>
        /// <param name="connectivityService">The internet connectivity service.</param>
        /// <param name="logService">The logging service.</param>
        /// <param name="notificationService">The notification service.</param>
        /// <param name="dialogService">The dialog service.</param>
        public AppInstallationCoordinatorService(
            IAppInstallationService appInstallationService,
            IInternetConnectivityService connectivityService,
            ILogService logService,
            INotificationService notificationService = null,
            IDialogService dialogService = null)
        {
            _appInstallationService = appInstallationService ?? throw new ArgumentNullException(nameof(appInstallationService));
            _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _notificationService = notificationService;
            _dialogService = dialogService;
        }

        /// <summary>
        /// Installs an application with connectivity monitoring and proper cancellation handling.
        /// </summary>
        /// <param name="appInfo">The application information.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the installation operation with the result.</returns>
        public async Task<InstallationCoordinationResult> InstallAppAsync(
            AppInfo appInfo,
            IProgress<TaskProgressDetail> progress,
            CancellationToken cancellationToken = default)
        {
            if (appInfo == null)
                throw new ArgumentNullException(nameof(appInfo));
                
            // Create a linked cancellation token source that will be cancelled if either:
            // 1. The original cancellation token is cancelled (user initiated)
            // 2. We detect a connectivity issue and cancel the installation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // Update status
            var initialStatus = $"Installing {appInfo.Name}...";
            RaiseStatusChanged(appInfo, initialStatus);
            
            // Start connectivity monitoring
            var connectivityMonitoringTask = StartConnectivityMonitoring(appInfo, linkedCts);
            
            try
            {
                // Perform the installation
                var installationResult = await _appInstallationService.InstallAppAsync(
                    appInfo, 
                    progress, 
                    linkedCts.Token);
                
                // Stop connectivity monitoring
                linkedCts.Cancel();
                try { await connectivityMonitoringTask; } catch (OperationCanceledException) { /* Expected */ }
                
                if (installationResult.Success)
                {
                    var successMessage = $"Successfully installed {appInfo.Name}";
                    RaiseStatusChanged(appInfo, successMessage, true, true);
                    
                    return new InstallationCoordinationResult
                    {
                        Success = true,
                        AppInfo = appInfo
                    };
                }
                else
                {
                    var errorMessage = installationResult.ErrorMessage ?? $"Failed to install {appInfo.Name}";
                    RaiseStatusChanged(appInfo, errorMessage, true, false);
                    
                    return new InstallationCoordinationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage,
                        AppInfo = appInfo
                    };
                }
            }
            catch (OperationCanceledException)
            {
                // Stop connectivity monitoring
                linkedCts.Cancel();
                try { await connectivityMonitoringTask; } catch (OperationCanceledException) { /* Expected */ }
                
                // Determine if this was a user cancellation or a connectivity issue
                bool wasConnectivityIssue = await connectivityMonitoringTask.ContinueWith(t => 
                {
                    // If the task completed normally, check its result
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        return t.Result;
                    }
                    // If the task was cancelled, assume it was a user cancellation
                    return false;
                });
                
                if (wasConnectivityIssue)
                {
                    var connectivityMessage = $"Installation of {appInfo.Name} was stopped due to internet connectivity issues";
                    RaiseStatusChanged(appInfo, connectivityMessage, true, false, false, true);
                    
                    return new InstallationCoordinationResult
                    {
                        Success = false,
                        WasCancelled = false,
                        WasConnectivityIssue = true,
                        ErrorMessage = "Internet connection lost during installation. Please check your network connection and try again.",
                        AppInfo = appInfo
                    };
                }
                else
                {
                    var cancelMessage = $"Installation of {appInfo.Name} was cancelled by user";
                    RaiseStatusChanged(appInfo, cancelMessage, true, false, true);
                    
                    return new InstallationCoordinationResult
                    {
                        Success = false,
                        WasCancelled = true,
                        WasConnectivityIssue = false,
                        ErrorMessage = "Installation was cancelled by user",
                        AppInfo = appInfo
                    };
                }
            }
            catch (Exception ex)
            {
                // Stop connectivity monitoring
                linkedCts.Cancel();
                try { await connectivityMonitoringTask; } catch (OperationCanceledException) { /* Expected */ }
                
                // Log the error
                _logService.LogError($"Error installing {appInfo.Name}: {ex.Message}", ex);
                
                // Check if the exception is related to internet connectivity
                bool isConnectivityIssue = ex.Message.Contains("internet") || 
                                         ex.Message.Contains("connection") || 
                                         ex.Message.Contains("network") ||
                                         ex.Message.Contains("pipeline has been stopped");
                
                string errorMessage = isConnectivityIssue
                    ? "Internet connection lost during installation. Please check your network connection and try again."
                    : ex.Message;
                
                RaiseStatusChanged(appInfo, $"Error installing {appInfo.Name}: {errorMessage}", true, false, false, isConnectivityIssue);
                
                return new InstallationCoordinationResult
                {
                    Success = false,
                    WasCancelled = false,
                    WasConnectivityIssue = isConnectivityIssue,
                    ErrorMessage = errorMessage,
                    AppInfo = appInfo
                };
            }
        }
        
        /// <summary>
        /// Starts monitoring internet connectivity during installation.
        /// </summary>
        /// <param name="appInfo">The application information.</param>
        /// <param name="cts">The cancellation token source.</param>
        /// <returns>A task that completes when monitoring is stopped, with a result indicating if a connectivity issue was detected.</returns>
        private async Task<bool> StartConnectivityMonitoring(AppInfo appInfo, CancellationTokenSource cts)
        {
            bool connectivityIssueDetected = false;
            
            // Set up connectivity change handler
            EventHandler<ConnectivityChangedEventArgs> connectivityChangedHandler = null;
            connectivityChangedHandler = (sender, args) =>
            {
                if (cts.Token.IsCancellationRequested)
                {
                    return; // Installation already cancelled
                }
                
                if (args.IsUserCancelled)
                {
                    // User cancelled the operation
                    var message = $"Installation of {appInfo.Name} was cancelled by user";
                    RaiseStatusChanged(appInfo, message, false, false, true);
                }
                else if (!args.IsConnected)
                {
                    // Internet connection lost
                    connectivityIssueDetected = true;
                    
                    var message = $"Error: Internet connection lost while installing {appInfo.Name}. Installation stopped.";
                    RaiseStatusChanged(appInfo, message, false, false, false, true);
                    
                    // Show a notification if available
                    _notificationService?.ShowToast(
                        "Internet Connection Lost",
                        "Internet connection has been lost during installation. Installation has been stopped.",
                        ToastType.Error
                    );
                    
                    // Show a dialog if available
                    if (_dialogService != null)
                    {
                        // Fire and forget - we don't want to block the connectivity handler
                        _ = _dialogService.ShowInformationAsync(
                            $"The installation of {appInfo.Name} has been stopped because internet connection was lost. Please check your network connection and try again when your internet connection is stable.",
                            "Internet Connection Lost"
                        );
                    }
                    
                    // Cancel the installation process
                    cts.Cancel();
                }
            };
            
            try
            {
                // Subscribe to connectivity changes
                _connectivityService.ConnectivityChanged += connectivityChangedHandler;
                
                // Start monitoring connectivity
                await _connectivityService.StartMonitoringAsync(5, cts.Token);
                
                // Wait for the cancellation token to be triggered
                try
                {
                    // This will throw when the token is cancelled
                    await Task.Delay(-1, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when installation completes or is cancelled
                }
                
                return connectivityIssueDetected;
            }
            finally
            {
                // Unsubscribe from connectivity changes
                if (connectivityChangedHandler != null)
                {
                    _connectivityService.ConnectivityChanged -= connectivityChangedHandler;
                }
                
                // Stop monitoring connectivity
                _connectivityService.StopMonitoring();
            }
        }
        
        /// <summary>
        /// Raises the InstallationStatusChanged event.
        /// </summary>
        /// <param name="appInfo">The application information.</param>
        /// <param name="statusMessage">The status message.</param>
        /// <param name="isComplete">Whether the installation is complete.</param>
        /// <param name="isSuccess">Whether the installation was successful.</param>
        /// <param name="isCancelled">Whether the installation was cancelled by the user.</param>
        /// <param name="isConnectivityIssue">Whether the installation failed due to connectivity issues.</param>
        private void RaiseStatusChanged(
            AppInfo appInfo,
            string statusMessage,
            bool isComplete = false,
            bool isSuccess = false,
            bool isCancelled = false,
            bool isConnectivityIssue = false)
        {
            _logService.LogInformation(statusMessage);
            
            InstallationStatusChanged?.Invoke(
                this,
                new InstallationStatusChangedEventArgs(
                    appInfo,
                    statusMessage,
                    isComplete,
                    isSuccess,
                    isCancelled,
                    isConnectivityIssue
                )
            );
        }
    }
}
