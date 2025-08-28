using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Models.Enums;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for system-level services that interact with the Windows operating system.
    /// </summary>
    public interface ISystemServices
    {
        /// <summary>
        /// Gets the registry service.
        /// </summary>
        IWindowsRegistryService WindowsRegistryService { get; }

        /// <summary>
        /// Restarts the Windows Explorer process.
        /// </summary>
        void RestartExplorer();

        /// <summary>
        /// Checks if the current user is an administrator.
        /// </summary>
        /// <returns>True if the current user is an administrator; otherwise, false.</returns>
        bool IsAdministrator();

        /// <summary>
        /// Gets the Windows version.
        /// </summary>
        /// <returns>A string representing the Windows version.</returns>
        string GetWindowsVersion();

        /// <summary>
        /// Gets the Windows build number.
        /// </summary>
        /// <returns>The Windows build number as an integer.</returns>
        int GetWindowsBuildNumber();

        /// <summary>
        /// Gets the friendly OS version string (e.g., "Windows 10" or "Windows 11")
        /// </summary>
        /// <returns>A friendly OS version string</returns>
        string GetOsVersionString();

        /// <summary>
        /// Gets the OS build number as a string
        /// </summary>
        /// <returns>The OS build number as a string</returns>
        string GetOsBuildString();

        /// <summary>
        /// Refreshes the desktop.
        /// </summary>
        void RefreshDesktop();

        /// <summary>
        /// Checks if a process is running.
        /// </summary>
        /// <param name="processName">The name of the process to check.</param>
        /// <returns>True if the process is running; otherwise, false.</returns>
        bool IsProcessRunning(string processName);

        /// <summary>
        /// Kills a process.
        /// </summary>
        /// <param name="processName">The name of the process to kill.</param>
        void KillProcess(string processName);

        /// <summary>
        /// Checks if the operating system is Windows 11.
        /// </summary>
        /// <returns>True if the operating system is Windows 11; otherwise, false.</returns>
        bool IsWindows11();

        /// <summary>
        /// Requires administrator privileges.
        /// </summary>
        /// <returns>True if the application is running with administrator privileges; otherwise, false.</returns>
        bool RequireAdministrator();

        /// <summary>
        /// Checks if dark mode is enabled.
        /// </summary>
        /// <returns>True if dark mode is enabled; otherwise, false.</returns>
        bool IsDarkModeEnabled();

        /// <summary>
        /// Sets dark mode.
        /// </summary>
        /// <param name="enabled">True to enable dark mode; false to disable it.</param>
        void SetDarkMode(bool enabled);

        /// <summary>
        /// Refreshes the Windows GUI.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the operation succeeded.</returns>
        Task<bool> RefreshWindowsGUI();

        /// <summary>
        /// Refreshes the Windows GUI.
        /// </summary>
        /// <param name="killExplorer">True to kill the Explorer process; otherwise, false.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the operation succeeded.</returns>
        Task<bool> RefreshWindowsGUI(bool killExplorer);

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
        Task<bool> IsInternetConnectedAsync(
            bool forceCheck = false,
            CancellationToken cancellationToken = default,
            bool userInitiatedCancellation = false
        );
    }
}
