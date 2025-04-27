using System.Threading.Tasks;
using Winhance.Core.Features.Customize.Enums;

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
        IRegistryService RegistryService { get; }

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
        /// Sets the UAC level.
        /// </summary>
        /// <param name="level">The UAC level to set.</param>
        void SetUacLevel(UacLevel level);

        /// <summary>
        /// Gets the UAC level.
        /// </summary>
        /// <returns>The current UAC level.</returns>
        UacLevel GetUacLevel();

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
    }
}
