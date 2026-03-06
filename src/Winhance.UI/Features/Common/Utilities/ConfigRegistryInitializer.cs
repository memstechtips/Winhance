using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Shared helper that ensures the compatible settings registry and global settings preloader
/// are initialized before config operations.
/// </summary>
internal static class ConfigRegistryInitializer
{
    public static async Task EnsureInitializedAsync(
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IGlobalSettingsPreloader settingsPreloader,
        ILogService logService)
    {
        if (!compatibleSettingsRegistry.IsInitialized)
        {
            logService.Log(LogLevel.Info, "Initializing compatible settings registry for configuration service");
            await compatibleSettingsRegistry.InitializeAsync();
        }

        if (!settingsPreloader.IsPreloaded)
        {
            logService.Log(LogLevel.Info, "Preloading settings for configuration service");
            await settingsPreloader.PreloadAllSettingsAsync();
        }
    }
}
