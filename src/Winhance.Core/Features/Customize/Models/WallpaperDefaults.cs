namespace Winhance.Core.Features.Customize.Models;

/// <summary>Default Windows wallpaper paths for the theme-mode setting (theme-mode-windows): the OS +
/// dark-mode conditional path logic the theme special handler applies on a Light/Dark switch. Lifted
/// VERBATIM out of the old WindowsThemeCustomizations def file at the SettingDefinition teardown.
///
/// TEMPORARY -- PLAN-5 (in-app documentation) absorbs this data into the catalog as a per-state
/// WallpaperEffect on theme-mode-windows' Light/Dark states, repoints ThemeWallpaperApplier onto the
/// catalog Setting it already resolves, and DELETES this file. Do not invest in it.</summary>
public static class WallpaperDefaults
{
    public const string Windows11BasePath = @"C:\Windows\Web\Wallpaper\Windows";
    public const string Windows11LightWallpaper = "img0.jpg";
    public const string Windows11DarkWallpaper = "img19.jpg";
    public const string Windows10Wallpaper =
        @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg";

    public static string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode)
    {
        if (isWindows11)
        {
            return System.IO.Path.Combine(
                Windows11BasePath,
                isDarkMode ? Windows11DarkWallpaper : Windows11LightWallpaper
            );
        }

        return Windows10Wallpaper;
    }
}
