using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;

namespace Winhance.Infrastructure.Features.Customize.Services;

internal sealed class ThemeWallpaperApplier(
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

        var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == SettingIds.ThemeModeWindows);
        if (catalogSetting is null)
        {
            logService.Log(LogLevel.Warning,
                "[ThemeWallpaperApplier] theme-mode-windows missing from the catalog - theme registry write skipped");
            return true;
        }

        // The option index IS the state index (Light = 0, Dark = 1), so resolve the state POSITIONALLY
        // rather than mapping the index back to a label. The Light/Dark states write the
        // AppsUseLightTheme + SystemUsesLightTheme DWORDs (Light -> 1, Dark -> 0 on both).
        //
        // A DETECT-ONLY state ("Mixed", index 2) is NOT an apply target: it carries no Set, and the
        // relationship reverse-sync hands this handler exactly that index when the two theme children
        // disagree. It is handled (return true) but writes nothing - the mix IS the children's own
        // states, and there is nothing for the master to write.
        var themeState = selectionIndex >= 0 && selectionIndex < catalogSetting.States.Count
            ? catalogSetting.States[selectionIndex]
            : null;
        if (themeState is null || themeState.IsDetectOnly)
        {
            logService.Log(LogLevel.Info,
                $"[ThemeWallpaperApplier] Index {selectionIndex} is not an applicable theme state - nothing written");
            return true;
        }

        var plan = ApplyPlan.From(ApplyPlanBuilder.Build(catalogSetting, themeState));
        var result = ApplyExecutor.Execute(plan, stateWriter);
        if (!result.AllSucceeded)
            logService.Log(LogLevel.Warning,
                $"[ThemeWallpaperApplier] {result.Failed}/{result.Total} theme write op(s) failed: {string.Join("; ", result.Failures)}");

        // This path is synchronous and cannot await, so a process-launching effect would be dropped.
        // ThemeWallpaperEffectsConformanceTests asserts none exists; this catches it at runtime if one does.
        if (plan.AsyncEffects.Count > 0)
            logService.Log(LogLevel.Error,
                $"[ThemeWallpaperApplier] {plan.AsyncEffects.Count} async effect(s) NOT run - a theme state gained one but this apply path is synchronous");

        // Import-flow checkbox: also change the wallpaper to match.
        if (additionalContext)
        {
            try
            {
                var liveBuild = new WinBuild(versionService.GetWindowsBuildNumber(), versionService.GetWindowsBuildRevision());
                var wallpaperPath = themeState.Effects
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
