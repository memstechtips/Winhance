namespace Winhance.Core.Features.Customize.Interfaces;

public interface IWallpaperService
{
    Task<bool> SetWallpaperAsync(string wallpaperPath);
}
