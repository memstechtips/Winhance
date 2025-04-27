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

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeService"/> class.
        /// </summary>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="wallpaperService">The wallpaper service.</param>
        public ThemeService(
            IRegistryService registryService,
            ILogService logService,
            IWallpaperService wallpaperService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _wallpaperService = wallpaperService ?? throw new ArgumentNullException(nameof(wallpaperService));
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

                // Refresh Windows GUI to apply changes
                await RefreshGUIAsync(true);

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
                // Implement GUI refresh directly instead of using ISystemServices
                const int HWND_BROADCAST = 0xffff;
                const int WM_SETTINGCHANGE = 0x001A;
                const int WM_SYSCOLORCHANGE = 0x0015;
                const int WM_THEMECHANGE = 0x031A;

                [DllImport("user32.dll", CharSet = CharSet.Auto)]
                static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

                [DllImport("user32.dll", CharSet = CharSet.Auto)]
                static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
                                                      uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

                SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOLORCHANGE, IntPtr.Zero, IntPtr.Zero);
                SendMessage((IntPtr)HWND_BROADCAST, WM_THEMECHANGE, IntPtr.Zero, IntPtr.Zero);
                
                if (restartExplorer)
                {
                    _logService.Log(LogLevel.Info, "Refreshing Windows GUI by terminating Explorer process");
                    
                    await Task.Delay(500);

                    bool explorerWasRunning = Process.GetProcessesByName("explorer").Length > 0;
                    
                    if (explorerWasRunning)
                    {
                        _logService.Log(LogLevel.Info, "Terminating Explorer processes - Windows will restart it automatically");
                        
                        foreach (var process in Process.GetProcessesByName("explorer"))
                        {
                            try
                            {
                                process.Kill();
                                _logService.Log(LogLevel.Info, $"Killed Explorer process (PID: {process.Id})");
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(LogLevel.Warning, $"Failed to kill Explorer process: {ex.Message}");
                            }
                        }
                        
                        _logService.Log(LogLevel.Info, "Waiting for Windows to automatically restart Explorer");
                        await Task.Delay(2000);
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Info, "Refreshing Windows GUI without killing Explorer");
                }
                
                string themeChanged = "ImmersiveColorSet";
                IntPtr themeChangedPtr = Marshal.StringToHGlobalUni(themeChanged);
                
                try
                {
                    IntPtr result;
                    SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, themeChangedPtr,
                                      0x0000, 1000, out result);
                    
                    SendMessage((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(themeChangedPtr);
                }

                _logService.Log(LogLevel.Info, "Windows GUI refresh completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing GUI: {ex.Message}");
                return false;
            }
        }
    }
}