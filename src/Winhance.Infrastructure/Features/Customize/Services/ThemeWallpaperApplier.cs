using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;

namespace Winhance.Infrastructure.Features.Customize.Services;

/// <summary>
/// Handles the wallpaper + registry side of the Windows theme switch. The explorer
/// refresh is no longer the handler's job — it's declarative via
/// <c>setting.RestartProcess = "Explorer"</c>, handled by ProcessRestartManager.
/// </summary>
public sealed class ThemeWallpaperApplier(
    IWallpaperService wallpaperService,
    IWindowsVersionService versionService,
    IStateWriter stateWriter,
    ILogService logService,
    IFileSystemService fileSystemService) : ISpecialSettingHandler
{
    public async Task<bool> TryApplySpecialSettingAsync(
        string settingId,
        object value,
        bool additionalContext = false,
        ISettingApplicationService? settingApplicationService = null)
    {
        if (settingId != SettingIds.ThemeModeWindows) return false;
        if (value is not int selectionIndex) return false;

        logService.Log(LogLevel.Info,
            $"[ThemeWallpaperApplier] Applying theme mode - Index: {selectionIndex}, ApplyWallpaper: {additionalContext}");

        // Light = 0, Dark = 1. Apply the matching catalog state: the theme-mode-windows Setting's
        // "Light Mode"/"Dark Mode" states write the AppsUseLightTheme + SystemUsesLightTheme DWORDs
        // (Light -> 1, Dark -> 0 on both).
        var stateLabel = selectionIndex == 1 ? "Dark Mode" : "Light Mode";
        var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == SettingIds.ThemeModeWindows);
        if (catalogSetting != null)
        {
            var result = ApplyExecutor.Execute(ApplyPlanBuilder.Build(catalogSetting, stateLabel), stateWriter);
            if (!result.AllSucceeded)
                logService.Log(LogLevel.Warning,
                    $"[ThemeWallpaperApplier] {result.Failed}/{result.Total} theme write op(s) failed: {string.Join("; ", result.Failures)}");
        }
        else
        {
            logService.Log(LogLevel.Warning,
                "[ThemeWallpaperApplier] theme-mode-windows missing from the catalog - theme registry write skipped");
        }

        // Import-flow checkbox: also change the wallpaper to match.
        if (additionalContext)
        {
            try
            {
                // The wallpaper for the applied state lives on the catalog as a build-gated WallpaperEffect:
                // pick the effect whose AppliesTo admits the live build.
                var themeState = catalogSetting?.States.FirstOrDefault(s => s.Label == stateLabel);
                var liveBuild = new WinBuild(versionService.GetWindowsBuildNumber(), versionService.GetWindowsBuildRevision());
                var wallpaperPath = themeState?.Effects
                    .OfType<WallpaperEffect>()
                    .FirstOrDefault(e => e.AppliesTo.Count == 0 || e.AppliesTo.Any(r => r.Contains(liveBuild)))
                    ?.Path;

                if (wallpaperPath != null && fileSystemService.FileExists(wallpaperPath))
                {
                    await wallpaperService.SetWallpaperAsync(wallpaperPath).ConfigureAwait(false);
                    logService.Log(LogLevel.Info, $"[ThemeWallpaperApplier] Wallpaper changed to: {wallpaperPath}");
                }
            }
            catch (System.Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ThemeWallpaperApplier] Failed to change wallpaper: {ex.Message}");
            }
        }

        return true;
    }
}
