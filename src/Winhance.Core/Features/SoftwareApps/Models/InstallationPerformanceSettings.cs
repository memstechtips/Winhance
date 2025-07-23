using System;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    /// <summary>
    /// Configuration settings for optimizing installation performance.
    /// Default settings are optimized for fast network performance.
    /// </summary>
    public class InstallationPerformanceSettings
    {
        /// <summary>
        /// Interval in seconds for connectivity checks during installation.
        /// Default: 15 seconds (optimized for fast performance).
        /// </summary>
        public int ConnectivityCheckIntervalSeconds { get; set; } = 15;

        /// <summary>
        /// Interval in seconds for progress reporting throttling.
        /// Default: 3 seconds for responsive UI updates.
        /// </summary>
        public int ProgressReportingIntervalSeconds { get; set; } = 3;

        /// <summary>
        /// Whether to perform offline capability detection before installation.
        /// Default: true to optimize network usage.
        /// </summary>
        public bool EnableOfflineCapabilityDetection { get; set; } = true;

        /// <summary>
        /// Whether to use batch installation optimizations.
        /// Default: true for better performance with multiple items.
        /// </summary>
        public bool EnableBatchOptimizations { get; set; } = true;

        /// <summary>
        /// Maximum number of concurrent installations.
        /// Default: 3 for maximum performance (fast network optimized).
        /// </summary>
        public int MaxConcurrentInstallations { get; set; } = 3;

        /// <summary>
        /// Timeout in minutes for individual installation operations.
        /// Default: 15 minutes (shorter timeout for fast networks).
        /// </summary>
        public int InstallationTimeoutMinutes { get; set; } = 15;

        /// <summary>
        /// Creates default performance settings optimized for typical installations.
        /// </summary>
        /// <returns>Default performance settings.</returns>
        public static InstallationPerformanceSettings CreateDefault()
        {
            return new InstallationPerformanceSettings();
        }

        /// <summary>
        /// Creates performance settings optimized for slow network connections.
        /// </summary>
        /// <returns>Performance settings for slow networks.</returns>
        public static InstallationPerformanceSettings CreateForSlowNetwork()
        {
            return new InstallationPerformanceSettings
            {
                ConnectivityCheckIntervalSeconds = 60, // Check less frequently
                ProgressReportingIntervalSeconds = 10, // Report less frequently
                MaxConcurrentInstallations = 1, // One at a time
                InstallationTimeoutMinutes = 60 // Longer timeout
            };
        }

        /// <summary>
        /// Creates performance settings optimized for fast network connections.
        /// </summary>
        /// <returns>Performance settings for fast networks.</returns>
        public static InstallationPerformanceSettings CreateForFastNetwork()
        {
            return new InstallationPerformanceSettings
            {
                ConnectivityCheckIntervalSeconds = 15, // Check more frequently
                ProgressReportingIntervalSeconds = 3, // Report more frequently
                MaxConcurrentInstallations = 3, // More concurrent installations
                InstallationTimeoutMinutes = 15 // Shorter timeout
            };
        }
    }
}
