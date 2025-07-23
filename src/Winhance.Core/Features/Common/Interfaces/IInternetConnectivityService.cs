using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for services that check and monitor internet connectivity.
    /// </summary>
    public interface IInternetConnectivityService
    {
        /// <summary>
        /// Checks if the system has an active internet connection.
        /// </summary>
        /// <param name="forceCheck">If true, bypasses the cache and performs a fresh check.</param>
        /// <returns>True if internet is connected, false otherwise.</returns>
        bool IsInternetConnected(bool forceCheck = false);

        /// <summary>
        /// Asynchronously checks if the system has an active internet connection.
        /// </summary>
        /// <param name="forceCheck">If true, bypasses the cache and performs a fresh check.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if internet is connected, false otherwise.</returns>
        Task<bool> IsInternetConnectedAsync(bool forceCheck = false, CancellationToken cancellationToken = default, bool userInitiatedCancellation = false);
        
        /// <summary>
        /// Event that is raised when the internet connectivity status changes.
        /// </summary>
        event EventHandler<ConnectivityChangedEventArgs> ConnectivityChanged;
        
        /// <summary>
        /// Starts monitoring internet connectivity during installation with optimized intervals.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
        /// <returns>A task representing the monitoring operation.</returns>
        Task StartInstallationMonitoringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts monitoring internet connectivity at the specified interval.
        /// </summary>
        /// <param name="intervalSeconds">The interval in seconds between connectivity checks (default: 15 seconds for fast performance).</param>
        /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
        /// <returns>A task representing the monitoring operation.</returns>
        Task StartMonitoringAsync(int intervalSeconds = 15, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Stops monitoring internet connectivity.
        /// </summary>
        void StopMonitoring();
    }
    
    /// <summary>
    /// Event arguments for connectivity change events.
    /// </summary>
    public class ConnectivityChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether the internet is connected.
        /// </summary>
        public bool IsConnected { get; }
        
        /// <summary>
        /// Gets a value indicating whether the connectivity change was due to user cancellation.
        /// </summary>
        public bool IsUserCancelled { get; }
        
        /// <summary>
        /// Gets the timestamp when the connectivity status changed.
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectivityChangedEventArgs"/> class.
        /// </summary>
        /// <param name="isConnected">Whether the internet is connected.</param>
        /// <param name="isUserCancelled">Whether the connectivity change was due to user cancellation.</param>
        public ConnectivityChangedEventArgs(bool isConnected, bool isUserCancelled = false)
        {
            IsConnected = isConnected;
            IsUserCancelled = isUserCancelled;
            Timestamp = DateTime.Now;
        }
    }
}
