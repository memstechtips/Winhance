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
        private readonly IInteractiveUserService _interactiveUserService;
        private readonly IWindowsRegistryService _registryService;

        // P/Invoke constants
        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public WallpaperService(
            ILogService logService,
            IInteractiveUserService interactiveUserService,
            IWindowsRegistryService registryService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _interactiveUserService = interactiveUserService;
            _registryService = registryService;
        }

        /// <inheritdoc/>
        public string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode)
        {
            return WindowsThemeCustomizations.Wallpaper.GetDefaultWallpaperPath(isWindows11, isDarkMode);
        }

        /// <inheritdoc/>
        public Task<bool> SetWallpaperAsync(string wallpaperPath)
        {
            try
            {
                int flags;

                if (_interactiveUserService.IsOtsElevation)
                {
                    // Under OTS, SPIF_UPDATEINIFILE would persist to the admin's profile.
                    // Write to the interactive user's registry instead, then only broadcast.
                    _registryService.SetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Desktop",
                        "Wallpaper",
                        wallpaperPath,
                        Microsoft.Win32.RegistryValueKind.String);

                    flags = SPIF_SENDCHANGE;
                }
                else
                {
                    flags = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;
                }

                bool success = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, wallpaperPath, flags) != 0;

                if (success)
                {
                    _logService.Log(LogLevel.Info, $"Wallpaper set to {wallpaperPath}");
                }
                else
                {
                    _logService.Log(LogLevel.Error, $"Failed to set wallpaper: {Marshal.GetLastWin32Error()}");
                }

                return Task.FromResult(success);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting wallpaper: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }
}