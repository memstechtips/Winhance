namespace Winhance.Core.Features.Common.Catalog;

// The Enabled state carries the Recommended/WindowsDefault role when the toggle should be on, Disabled when off.
// WindowsDefault can differ per OS on a merged setting (This PC folders: shown on Win10, hidden on Win11), so
// the lookup is build-aware.
public static class CatalogToggleState
{
    public static bool? GetRecommended(Setting setting, WinBuild build) => ForRole(setting, RoleKind.Recommended, build);

    public static bool? GetDefault(Setting setting, WinBuild build) => ForRole(setting, RoleKind.WindowsDefault, build);

    private static bool? ForRole(Setting setting, RoleKind role, WinBuild build)
    {
        var enabled = setting.States.FirstOrDefault(s => s.Label == "Enabled");
        if (enabled is not null && enabled.HasRole(role, build)) return true;
        var disabled = setting.States.FirstOrDefault(s => s.Label == "Disabled");
        if (disabled is not null && disabled.HasRole(role, build)) return false;
        return null;
    }
}
