using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Coordinates the installation of applications, handling connectivity monitoring,
    /// user notifications, and installation state management.
    /// </summary>
    public interface IAppInstallationCoordinatorService
    {
        /// <summary>
        /// Installs an application with connectivity monitoring and proper cancellation handling.
        /// </summary>
        /// <param name="appInfo">The application information.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the installation operation with the result.</returns>
        Task<InstallationCoordinationResult> InstallAppAsync(
            AppInfo appInfo,
            IProgress<TaskProgressDetail> progress,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Event that is raised when the installation status changes.
        /// </summary>
        event EventHandler<InstallationStatusChangedEventArgs> InstallationStatusChanged;
    }
    
    /// <summary>
    /// Result of an installation coordination operation.
    /// </summary>
    public class InstallationCoordinationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the installation was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the error message if the installation failed.
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the installation was cancelled by the user.
        /// </summary>
        public bool WasCancelled { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the installation failed due to connectivity issues.
        /// </summary>
        public bool WasConnectivityIssue { get; set; }
        
        /// <summary>
        /// Gets or sets the application information.
        /// </summary>
        public AppInfo AppInfo { get; set; }
    }
    
    /// <summary>
    /// Event arguments for installation status change events.
    /// </summary>
    public class InstallationStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the application information.
        /// </summary>
        public AppInfo AppInfo { get; }
        
        /// <summary>
        /// Gets the status message.
        /// </summary>
        public string StatusMessage { get; }
        
        /// <summary>
        /// Gets a value indicating whether the installation is complete.
        /// </summary>
        public bool IsComplete { get; }
        
        /// <summary>
        /// Gets a value indicating whether the installation was successful.
        /// </summary>
        public bool IsSuccess { get; }
        
        /// <summary>
        /// Gets a value indicating whether the installation was cancelled by the user.
        /// </summary>
        public bool IsCancelled { get; }
        
        /// <summary>
        /// Gets a value indicating whether the installation failed due to connectivity issues.
        /// </summary>
        public bool IsConnectivityIssue { get; }
        
        /// <summary>
        /// Gets the timestamp when the status changed.
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationStatusChangedEventArgs"/> class.
        /// </summary>
        /// <param name="appInfo">The application information.</param>
        /// <param name="statusMessage">The status message.</param>
        /// <param name="isComplete">Whether the installation is complete.</param>
        /// <param name="isSuccess">Whether the installation was successful.</param>
        /// <param name="isCancelled">Whether the installation was cancelled by the user.</param>
        /// <param name="isConnectivityIssue">Whether the installation failed due to connectivity issues.</param>
        public InstallationStatusChangedEventArgs(
            AppInfo appInfo,
            string statusMessage,
            bool isComplete = false,
            bool isSuccess = false,
            bool isCancelled = false,
            bool isConnectivityIssue = false)
        {
            AppInfo = appInfo;
            StatusMessage = statusMessage;
            IsComplete = isComplete;
            IsSuccess = isSuccess;
            IsCancelled = isCancelled;
            IsConnectivityIssue = isConnectivityIssue;
            Timestamp = DateTime.Now;
        }
    }
}
