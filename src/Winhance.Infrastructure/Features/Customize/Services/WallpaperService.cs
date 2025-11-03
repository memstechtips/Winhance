using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    /// <summary>
    /// Service for wallpaper operations.
    /// </summary>
    public class WallpaperService : IWallpaperService
    {
        private readonly ILogService _logService;

        // P/Invoke constants
        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        /// <summary>
        /// Initializes a new instance of the <see cref="WallpaperService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public WallpaperService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode)
        {
            return WindowsThemeCustomizations.Wallpaper.GetDefaultWallpaperPath(isWindows11, isDarkMode);
        }

        /// <inheritdoc/>
        public async Task<bool> SetDefaultWallpaperAsync(bool isWindows11, bool isDarkMode)
        {
            string wallpaperPath = GetDefaultWallpaperPath(isWindows11, isDarkMode);
            return await SetWallpaperAsync(wallpaperPath);
        }

        /// <inheritdoc/>
        public async Task<bool> SetWallpaperAsync(string wallpaperPath)
        {
            try
            {
                bool success = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, wallpaperPath,
                                                  SPIF_UPDATEINIFILE | SPIF_SENDCHANGE) != 0;

                if (success)
                {
                    _logService.Log(LogLevel.Info, $"Wallpaper set to {wallpaperPath}");
                }
                else
                {
                    _logService.Log(LogLevel.Error, $"Failed to set wallpaper: {Marshal.GetLastWin32Error()}");
                }

                await Task.CompletedTask; // To keep the async signature
                return success;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting wallpaper: {ex.Message}");
                return false;
            }
        }
    }
}