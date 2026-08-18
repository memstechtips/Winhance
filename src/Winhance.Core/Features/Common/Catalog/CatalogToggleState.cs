namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Resolves the recommended/default toggle state of a TOGGLE setting for a live build. The "Enabled" state
/// carries the Recommended / WindowsDefault role when the toggle should be on, the "Disabled" state when it
/// should be off, so reading the role back off the state gives the bool. WindowsDefault can be OS-divergent
/// on a merged setting (the This PC folders default to shown on Windows 10, hidden on Windows 11), so the
/// lookup is build-aware. A setting with no two-state Enabled/Disabled shape resolves to null.
/// </summary>
public static class CatalogToggleState
{
    /// <summary>The recommended toggle state on the given build: true when the "Enabled" state carries the
    /// Recommended role, false when the "Disabled" state does, null when neither does (or it is not a two-state
    /// Enabled/Disabled toggle).</summary>
    public static bool? GetRecommended(Setting setting, WinBuild build) => ForRole(setting, RoleKind.Recommended, build);

    /// <summary>The Windows-default toggle state on the given build, derived the same way from the WindowsDefault
    /// role (which may be build-scoped on a merged setting).</summary>
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
