using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Shared helper that ensures the compatible settings registry is initialized
/// before config operations.
/// </summary>
internal static class ConfigRegistryInitializer
{
    public static async Task EnsureInitializedAsync(
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ILogService logService)
    {
        if (!compatibleSettingsRegistry.IsInitialized)
        {
            logService.Log(LogLevel.Info, "Initializing compatible settings registry for configuration service");
            await compatibleSettingsRegistry.InitializeAsync();
        }
    }
}
