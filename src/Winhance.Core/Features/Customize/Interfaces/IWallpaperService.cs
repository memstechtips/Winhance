using System.Threading.Tasks;

namespace Winhance.Core.Features.Customize.Interfaces;

/// <summary>
/// Interface for wallpaper operations.
/// </summary>
public interface IWallpaperService
{
    /// <summary>
    /// Sets the desktop wallpaper.
    /// </summary>
    /// <param name="wallpaperPath">The path to the wallpaper image.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<bool> SetWallpaperAsync(string wallpaperPath);

    /// <summary>
    /// Gets the default wallpaper path based on Windows version and theme.
    /// </summary>
    /// <param name="isWindows11">Whether the system is Windows 11.</param>
    /// <param name="isDarkMode">Whether dark mode is enabled.</param>
    /// <returns>The path to the default wallpaper.</returns>
    string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode);
}
