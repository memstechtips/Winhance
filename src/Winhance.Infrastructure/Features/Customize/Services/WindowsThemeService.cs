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
        ILogService logService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry) : IDomainService
    {
        public string DomainName => FeatureIds.WindowsTheme;

        public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false)
        {
            if (setting.Id == "theme-mode-windows" && value is int index)
            {
                await ApplyThemeModeWindowsAsync(setting, index, additionalContext);
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                return compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.WindowsTheme);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Windows theme settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        public async Task ApplyThemeModeWindowsAsync(SettingDefinition setting, object value, bool applyWallpaper = false)
        {
            if (value is not int selectionIndex)
                throw new ArgumentException("Expected integer selection index for theme mode");

            logService.Log(LogLevel.Info, $"[WindowsThemeService] Applying theme mode - Index: {selectionIndex}, ApplyWallpaper: {applyWallpaper}");

            if (setting.RegistrySettings?.Count > 0)
            {
                logService.Log(LogLevel.Info, $"[WindowsThemeService] Applying {setting.RegistrySettings.Count} registry settings");

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    int themeValue = selectionIndex == 1 ? 0 : 1;
                    registryService.ApplySetting(registrySetting, true, themeValue);
                }
            }

            if (applyWallpaper)
            {
                logService.Log(LogLevel.Info, $"[WindowsThemeService] Applying wallpaper for theme mode");
                await ApplyWallpaperForTheme(selectionIndex);
            }

            logService.Log(LogLevel.Info, $"[WindowsThemeService] Refreshing Windows UI");
            await RefreshWindowsUI();

            logService.Log(LogLevel.Info, $"[WindowsThemeService] Successfully applied theme mode");
        }

        private async Task ApplyWallpaperForTheme(int selectionIndex)
        {
            try
            {
                var isDarkMode = selectionIndex == 1;
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
                bool shouldKillExplorer = !uiManagementService.IsConfigImportMode;
                await uiManagementService.RefreshWindowsGUI(killExplorer: shouldKillExplorer);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to refresh Windows GUI: {ex.Message}");
            }
        }
    }
}
