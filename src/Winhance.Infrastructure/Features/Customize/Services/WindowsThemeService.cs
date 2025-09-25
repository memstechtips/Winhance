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
    public class WindowsThemeService(
        IWallpaperService wallpaperService,
        IWindowsVersionService versionService,
        IWindowsUIManagementService uiManagementService,
        IWindowsRegistryService registryService,
        ILogService logService) : IDomainService
    {
        public string DomainName => FeatureIds.WindowsTheme;

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                var group = WindowsThemeCustomizations.GetWindowsThemeCustomizations();
                return group.Settings;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Windows theme settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        public async Task ApplySettingWithContextAsync(string settingId, bool enable, object? value, SettingOperationContext context)
        {
            logService.Log(LogLevel.Info, $"[WindowsThemeService] ApplySettingWithContextAsync called - Enable: {enable}, ApplyWallpaper: {context.ApplyWallpaper}");
            
            var settings = await GetSettingsAsync();
            var setting = settings.FirstOrDefault(s => s.Id == settingId);
            if (setting == null)
                throw new ArgumentException($"Setting '{settingId}' not found");

            ApplyRegistryChanges(setting, context.RegistryValues);

            if (enable && context.ApplyWallpaper)
            {
                logService.Log(LogLevel.Info, $"[WindowsThemeService] Applying wallpaper for theme change");
                await ApplyWallpaperForTheme(value);
            }

            logService.Log(LogLevel.Info, $"[WindowsThemeService] Refreshing Windows UI");
            await RefreshWindowsUI();
        }

        private void ApplyRegistryChanges(SettingDefinition setting, Dictionary<string, int?>? resolvedValues)
        {
            if (setting.RegistrySettings?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[WindowsThemeService] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}'");

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    if (resolvedValues?.TryGetValue(registrySetting.ValueName, out var specificValue) == true)
                    {
                        if (specificValue == null)
                        {
                            registryService.ApplySetting(registrySetting, false);
                        }
                        else
                        {
                            registryService.ApplySetting(registrySetting, true, specificValue.Value);
                        }
                    }
                    else
                    {
                        registryService.ApplySetting(registrySetting, true);
                    }
                }
            }
        }

        private async Task ApplyWallpaperForTheme(object? value)
        {
            try
            {
                var isDarkMode = value is int comboBoxIndex ? comboBoxIndex == 1 : false;
                var isWindows11 = versionService.IsWindows11();
                var wallpaperPath = WindowsThemeCustomizations.Wallpaper.GetDefaultWallpaperPath(isWindows11, isDarkMode);

                if (System.IO.File.Exists(wallpaperPath))
                {
                    await wallpaperService.SetWallpaperAsync(wallpaperPath);
                    logService.Log(LogLevel.Info, $"Wallpaper changed to: {wallpaperPath}");
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to change wallpaper: {ex.Message}");
            }
        }

        private async Task RefreshWindowsUI()
        {
            try
            {
                await uiManagementService.RefreshWindowsGUI();
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to refresh Windows GUI: {ex.Message}");
            }
        }
    }
}
