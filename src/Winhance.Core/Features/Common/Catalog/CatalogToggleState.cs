using System.Linq;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// Catalog-model replacement for the old <c>SettingDefinitionToggleState</c> recommended/default resolution of a
/// TOGGLE setting, resolved for a live build. The converter assigned the catalog toggle states their
/// Recommended / WindowsDefault roles FROM SettingDefinitionToggleState (SettingDefinitionConverter.RolesFor:
/// the "Enabled" state carries the role when the old toggle bool is true, the "Disabled" state when it is false),
/// so reading the role back off the catalog state is the exact inverse. WindowsDefault can be OS-divergent on a
/// merged setting (the This PC folders default to shown on Windows 10, hidden on Windows 11), so the lookup is
/// build-aware. A setting with no two-state Enabled/Disabled shape resolves to null - matching the old helper.
/// Proven == the old helper over the whole paired population at migration by the now-retired RecommendedToggleStateConformanceTests.
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
