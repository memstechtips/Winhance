using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    /// <summary>
    /// Service implementation for managing Windows theme settings.
    /// Handles dark/light mode, transparency effects, and wallpaper changes.
    /// </summary>
    public class WindowsThemeService : IWindowsThemeService
    {
        private readonly IRegistryService _registryService;
        private readonly IWallpaperService _wallpaperService;
        private readonly ILogService _logService;
        private readonly ISystemServices _systemServices;

        public string DomainName => "WindowsTheme";

        public WindowsThemeService(
            IRegistryService registryService,
            IWallpaperService wallpaperService,
            ILogService logService,
            ISystemServices systemServices)
        {
            _registryService =
                registryService ?? throw new ArgumentNullException(nameof(registryService));
            _wallpaperService =
                wallpaperService ?? throw new ArgumentNullException(nameof(wallpaperService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Windows theme settings");
                
                var group = WindowsThemeSettings.GetWindowsThemeCustomizations();
                return group.Settings;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Windows theme settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying Windows theme setting '{settingId}': enable={enable}"
                );

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException(
                        $"Setting '{settingId}' not found in Windows theme domain"
                    );
                }

                // Apply registry settings
                if (setting.RegistrySettings?.Count > 0)
                {
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        await _registryService.ApplySettingAsync(registrySetting, enable);
                    }
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Successfully applied Windows theme setting '{settingId}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying Windows theme setting '{settingId}': {ex.Message}"
                );
                throw;
            }
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting?.RegistrySettings?.Count > 0)
                {
                    var status = await _registryService.GetSettingStatusAsync(setting.RegistrySettings[0]);
                    return status == RegistrySettingStatus.Applied;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking Windows theme setting '{settingId}': {ex.Message}");
                return false;
            }
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting?.RegistrySettings?.Count > 0)
                {
                    return await _registryService.GetCurrentValueAsync(setting.RegistrySettings[0]);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting Windows theme setting value '{settingId}': {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetCurrentThemeStateAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting current Windows theme state");

                // Check if apps use light theme (0 = dark, 1 = light)
                var appsUseLightTheme = _registryService.GetValue("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme");

                // Check if system uses light theme (0 = dark, 1 = light)
                var systemUsesLightTheme = _registryService.GetValue("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "SystemUsesLightTheme");

                // If both are 0 (or null defaults to dark), it's dark mode
                bool isDarkMode = (appsUseLightTheme as int? ?? 0) == 0 && 
                                 (systemUsesLightTheme as int? ?? 0) == 0;

                var themeState = isDarkMode ? "Dark Mode" : "Light Mode";
                _logService.Log(LogLevel.Info, $"Current Windows theme state: {themeState}");
                
                return themeState;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting current Windows theme state: {ex.Message}");
                return "Unknown";
            }
        }

        public async Task<bool> ApplyThemeAsync(bool isDarkMode, bool changeWallpaper = false)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying Windows theme: isDarkMode={isDarkMode}, changeWallpaper={changeWallpaper}");

                // Set theme values (0 = dark, 1 = light)
                int themeValue = isDarkMode ? 0 : 1;

                // Apply apps theme setting
                bool appsSuccess = _registryService.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    themeValue,
                    RegistryValueKind.DWord);

                // Apply system theme setting
                bool systemSuccess = _registryService.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "SystemUsesLightTheme",
                    themeValue,
                    RegistryValueKind.DWord);
                
                if (!appsSuccess || !systemSuccess)
                {
                    _logService.Log(LogLevel.Error, "Failed to apply theme registry settings");
                    return false;
                }

                // Change wallpaper if requested
                if (changeWallpaper)
                {
                    try
                    {
                        var isWindows11 = _systemServices.IsWindows11();
                        var wallpaperPath = WindowsThemeSettings.Wallpaper.GetDefaultWallpaperPath(isWindows11, isDarkMode);
                        
                        if (System.IO.File.Exists(wallpaperPath))
                        {
                            await _wallpaperService.SetWallpaperAsync(wallpaperPath);
                            _logService.Log(LogLevel.Info, $"Wallpaper changed to: {wallpaperPath}");
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning, $"Wallpaper file not found: {wallpaperPath}");
                        }
                    }
                    catch (Exception wallpaperEx)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to change wallpaper: {wallpaperEx.Message}");
                        // Don't throw - wallpaper change is optional
                    }
                }

                _logService.Log(LogLevel.Info, "Windows theme applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying Windows theme: {ex.Message}");
                return false;
            }
        }

        public bool IsDarkModeEnabled()
        {
            try
            {
                string keyPath = "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
                var value = _registryService.GetValue(keyPath, "AppsUseLightTheme");
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

        public string GetCurrentThemeName()
        {
            return IsDarkModeEnabled() ? "Dark Mode" : "Light Mode";
        }

        public bool SetThemeMode(bool isDarkMode)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Setting theme mode to {(isDarkMode ? "dark" : "light")}");

                // Use the settings system instead of manual registry calls
                // This fixes the HKCU duplication issue and follows DDD principles
                var task = ApplySettingAsync("windows-theme-mode", isDarkMode);
                task.Wait(); // Convert async to sync for interface compatibility
                
                _logService.Log(LogLevel.Success, $"Theme mode set to {(isDarkMode ? "dark" : "light")}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error setting theme mode: {ex.Message}");
                return false;
            }
        }

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
