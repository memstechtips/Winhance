using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
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
    public class WindowsThemeService : IDomainService
    {
        private readonly IWallpaperService _wallpaperService;
        private readonly ISystemServices _systemServices;
        private readonly SettingControlHandler _controlHandler;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly ILogService _logService;

        public string DomainName => FeatureIds.WindowsTheme;

        public WindowsThemeService(
            IWallpaperService wallpaperService,
            ISystemServices systemServices,
            SettingControlHandler controlHandler,
            ISystemSettingsDiscoveryService discoveryService,
            ILogService logService
        )
        {
            _wallpaperService =
                wallpaperService ?? throw new ArgumentNullException(nameof(wallpaperService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _controlHandler = controlHandler ?? throw new ArgumentNullException(nameof(controlHandler));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                var group = WindowsThemeCustomizations.GetWindowsThemeCustomizations();
                return await _discoveryService.GetSettingsWithSystemStateAsync(group.Settings, DomainName);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Windows theme settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            await ApplySettingWithContextAsync(settingId, enable, value, true);
        }

        public async Task ApplySettingWithContextAsync(string settingId, bool enable, object? value, bool applyWallpaper)
        {
            var settings = await GetRawSettingsAsync();
            var setting = settings.FirstOrDefault(s => s.Id == settingId);
            if (setting == null)
                throw new ArgumentException($"Setting '{settingId}' not found");

            switch (setting.InputType)
            {
                case SettingInputType.Toggle:
                    await _controlHandler.ApplyBinaryToggleAsync(setting, enable);
                    break;
                case SettingInputType.Selection when value is int index:
                    await _controlHandler.ApplyComboBoxIndexAsync(setting, index);
                    break;
                case SettingInputType.NumericRange when value != null:
                    await _controlHandler.ApplyNumericUpDownAsync(setting, value);
                    break;
                default:
                    throw new NotSupportedException($"Input type '{setting.InputType}' not supported");
            }

            if (settingId == "theme-mode-windows")
            {
                if (enable && applyWallpaper)
                {
                    try
                    {
                        var isDarkMode = value is int comboBoxIndex ? comboBoxIndex == 1 : false;
                        var isWindows11 = _systemServices.IsWindows11();
                        var wallpaperPath = WindowsThemeCustomizations.Wallpaper.GetDefaultWallpaperPath(isWindows11, isDarkMode);

                        if (System.IO.File.Exists(wallpaperPath))
                        {
                            await _wallpaperService.SetWallpaperAsync(wallpaperPath);
                            _logService.Log(LogLevel.Info, $"Wallpaper changed to: {wallpaperPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to change wallpaper: {ex.Message}");
                    }
                }

                try
                {
                    await _systemServices.RefreshWindowsGUI();
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Failed to refresh Windows GUI: {ex.Message}");
                }
            }
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingStatusAsync(settingId, settings);
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingValueAsync(settingId, settings);
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
        public async Task<IEnumerable<SettingDefinition>> GetRawSettingsAsync()
        {
            var group = WindowsThemeCustomizations.GetWindowsThemeCustomizations();
            return await Task.FromResult(group.Settings);
        }
    }
}
