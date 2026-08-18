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
}
