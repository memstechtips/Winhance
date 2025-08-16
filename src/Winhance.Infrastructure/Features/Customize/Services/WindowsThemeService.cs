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
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    /// <summary>
    /// Service implementation for managing Windows theme settings.
    /// Handles dark/light mode, transparency effects, and wallpaper changes.
    /// Maintains exact same method signatures and behavior for compatibility.
    /// </summary>
    public class WindowsThemeService : IWindowsThemeService
    {
        private readonly IWallpaperService _wallpaperService;
        private readonly ISystemServices _systemServices;
        private readonly SystemSettingOrchestrator _orchestrator;
        private readonly ILogService _logService;

        public string DomainName => "WindowsTheme";

        public WindowsThemeService(
            IWallpaperService wallpaperService,
            ISystemServices systemServices,
            SystemSettingOrchestrator orchestrator,
            ILogService logService
        )
        {
            _wallpaperService =
                wallpaperService ?? throw new ArgumentNullException(nameof(wallpaperService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _orchestrator =
                orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _logService =
                logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            var group = WindowsThemeSettings.GetWindowsThemeCustomizations();
            return await _orchestrator.GetSettingsWithSystemStateAsync(group.Settings, DomainName);
        }

        /// <summary>
        /// Applies a setting with theme-specific behavior.
        /// </summary>
        public async Task ApplySettingAsync(
            string settingId,
            bool enable,
            object? value = null
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying Windows theme setting '{settingId}': enable={enable}, value={value}"
                );

                // Get settings and apply using orchestrator
                var settings = await GetRawSettingsAsync();
                await _orchestrator.ApplySettingAsync(settingId, enable, value, settings, DomainName);

                // Theme-specific post-processing
                if (settingId == "theme-mode-windows")
                {
                    // Apply wallpaper if enabled
                    if (enable)
                    {
                        try
                        {
                            var isDarkMode = value is int comboBoxIndex ? comboBoxIndex == 0 : true;
                            var isWindows11 = _systemServices.IsWindows11();
                            var wallpaperPath =
                                WindowsThemeSettings.Wallpaper.GetDefaultWallpaperPath(
                                    isWindows11,
                                    isDarkMode
                                );

                            if (System.IO.File.Exists(wallpaperPath))
                            {
                                await _wallpaperService.SetWallpaperAsync(wallpaperPath);
                                _logService.Log(
                                    LogLevel.Info,
                                    $"Wallpaper changed to: {wallpaperPath}"
                                );
                            }
                            else
                            {
                                _logService.Log(
                                    LogLevel.Warning,
                                    $"Wallpaper file not found: {wallpaperPath}"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                $"Failed to change wallpaper: {ex.Message}"
                            );
                            // Don't throw - wallpaper change is optional
                        }
                    }

                    // Refresh Windows GUI to apply theme changes
                    try
                    {
                        _logService.Log(
                            LogLevel.Info,
                            "Refreshing Windows GUI to apply theme changes"
                        );
                        var refreshResult = await _systemServices.RefreshWindowsGUI();

                        if (refreshResult)
                        {
                            _logService.Log(LogLevel.Info, "Windows GUI successfully refreshed");
                        }
                        else
                        {
                            _logService.Log(
                                LogLevel.Warning,
                                "Windows GUI refresh completed but may not have been fully successful"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(
                            LogLevel.Warning,
                            $"Failed to refresh Windows GUI: {ex.Message}"
                        );
                        // Don't throw - GUI refresh failure shouldn't prevent theme change completion
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
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingStatusAsync(settingId, settings);
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _orchestrator.GetSettingValueAsync(settingId, settings);
        }

        public bool IsDarkModeEnabled()
        {
            try
            {
                // Delegate to domain service method to eliminate registry query redundancy
                // This ensures single source of truth and follows DRY principle
                // Registry operations are inherently synchronous, so we use the sync path
                var task = IsSettingEnabledAsync("theme-mode-windows");
                return task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking dark mode status: {ex.Message}");
                return false; // Default to light mode on error
            }
        }

        /// <summary>
        /// Helper method to get raw settings without system state.
        /// </summary>
        private async Task<IEnumerable<ApplicationSetting>> GetRawSettingsAsync()
        {
            var group = WindowsThemeSettings.GetWindowsThemeCustomizations();
            return await Task.FromResult(group.Settings);
        }
    }
}
