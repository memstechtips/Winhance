using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Customize.Interfaces
{
    /// <summary>
    /// Service interface for managing Windows theme settings.
    /// Handles dark/light mode, transparency effects, and wallpaper changes.
    /// </summary>
    public interface IWindowsThemeService : IDomainService
    {
        /// <summary>
        /// Checks if dark mode is enabled.
        /// </summary>
        /// <returns>True if dark mode is enabled; otherwise, false.</returns>
        bool IsDarkModeEnabled();

        /// <summary>
        /// Sets the theme mode.
        /// </summary>
        /// <param name="isDarkMode">True to enable dark mode; false to enable light mode.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        bool SetThemeMode(bool isDarkMode);

        /// <summary>
        /// Gets the current Windows theme state from the system.
        /// </summary>
        /// <returns>The current Windows theme state (Dark/Light).</returns>
        Task<string> GetCurrentThemeStateAsync();

        /// <summary>
        /// Gets the name of the current theme.
        /// </summary>
        /// <returns>The name of the current theme ("Light Mode" or "Dark Mode").</returns>
        string GetCurrentThemeName();

        /// <summary>
        /// Applies Windows theme changes (Dark/Light mode) to the system.
        /// </summary>
        /// <param name="isDarkMode">True for dark mode, false for light mode.</param>
        /// <param name="changeWallpaper">Whether to change wallpaper along with theme.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<bool> ApplyThemeAsync(bool isDarkMode, bool changeWallpaper = false);

        /// <summary>
        /// Refreshes the Windows GUI to apply theme changes.
        /// </summary>
        /// <param name="restartExplorer">True to restart Explorer; otherwise, false.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<bool> RefreshGUIAsync(bool restartExplorer);
    }
}
