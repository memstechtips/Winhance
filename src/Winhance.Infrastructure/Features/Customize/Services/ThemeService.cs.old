using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    /// <summary>
    /// Service for theme-related operations.
    /// </summary>
    public class ThemeService : IThemeService
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;
        private readonly IWallpaperService _wallpaperService;
        private readonly ISystemServices _systemServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeService"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="wallpaperService">The wallpaper service.</param>
        /// <param name="systemServices">The system services.</param>
        public ThemeService(
            IRegistryService registryService,
            ILogService logService,
            IWallpaperService wallpaperService,
            ISystemServices systemServices)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _wallpaperService = wallpaperService ?? throw new ArgumentNullException(nameof(wallpaperService));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
        }

        /// <inheritdoc/>
        public bool IsDarkModeEnabled()
        {
            try
            {
                string keyPath = $"HKCU\\{WindowsThemeSettings.Registry.ThemesPersonalizeSubKey}";
                var value = _registryService.GetValue(keyPath, WindowsThemeSettings.Registry.AppsUseLightThemeName);
                bool isDarkMode = value != null && (int)value == 0;

                _logService.Log(LogLevel.Info, $"Dark mode check completed. Is Dark Mode: {isDarkMode}");
                return isDarkMode;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking dark mode status: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public string GetCurrentThemeName()
        {
            return IsDarkModeEnabled() ? "Dark Mode" : "Light Mode";
        }

        /// <inheritdoc/>
        public bool SetThemeMode(bool isDarkMode)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Setting theme mode to {(isDarkMode ? "dark" : "light")}");

                string keyPath = $"HKCU\\{WindowsThemeSettings.Registry.ThemesPersonalizeSubKey}";
                int valueToSet = isDarkMode ? 0 : 1;

                // Set both registry values
                bool appsSuccess = _registryService.SetValue(
                    keyPath,
                    WindowsThemeSettings.Registry.AppsUseLightThemeName,
                    valueToSet,
                    Microsoft.Win32.RegistryValueKind.DWord);

                bool systemSuccess = _registryService.SetValue(
                    keyPath,
                    WindowsThemeSettings.Registry.SystemUsesLightThemeName,
                    valueToSet,
                    Microsoft.Win32.RegistryValueKind.DWord);

                bool success = appsSuccess && systemSuccess;
                if (success)
                {
                    _logService.Log(LogLevel.Success, $"Theme mode set to {(isDarkMode ? "dark" : "light")}");
                }
                else
                {
                    _logService.Log(LogLevel.Error, "Failed to set theme mode");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting theme mode: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ApplyThemeAsync(bool isDarkMode, bool changeWallpaper)
        {
            try
            {
                // Apply theme in registry
                bool themeSuccess = SetThemeMode(isDarkMode);
                if (!themeSuccess)
                {
                    return false;
                }

                // Change wallpaper if requested
                if (changeWallpaper)
                {
                    _logService.Log(LogLevel.Info, $"Changing wallpaper for {(isDarkMode ? "dark" : "light")} mode");
                    // Check Windows version directly instead of using ISystemServices
                    bool isWindows11 = Environment.OSVersion.Version.Build >= 22000;
                    await _wallpaperService.SetDefaultWallpaperAsync(isWindows11, isDarkMode);
                }

                // Use the improved RefreshWindowsGUI method to refresh the UI
                bool refreshResult = await _systemServices.RefreshWindowsGUI(true);
                if (!refreshResult)
                {
                    _logService.Log(LogLevel.Warning, "Failed to refresh Windows GUI after applying theme");
                    return false;
                }

                _logService.Log(LogLevel.Info, $"Theme applied successfully: {(isDarkMode ? "Dark" : "Light")} Mode");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying theme: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RefreshGUIAsync(bool restartExplorer)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Refreshing GUI with WindowsSystemService (restartExplorer: {restartExplorer})");
                
                // Use the improved implementation from WindowsSystemService
                bool result = await _systemServices.RefreshWindowsGUI(restartExplorer);
                
                if (result)
                {
                    _logService.Log(LogLevel.Info, "Windows GUI refresh completed successfully");
                }
                else
                {
                    _logService.Log(LogLevel.Error, "Failed to refresh Windows GUI");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing GUI: {ex.Message}");
                return false;
            }
        }
    }
}