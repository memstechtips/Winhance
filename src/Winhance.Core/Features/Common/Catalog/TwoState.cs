using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public static class TwoState
{
    public static bool Is(ControlKind kind) => kind is ControlKind.Toggle or ControlKind.CheckBox;

    public static LocKey OnLabel(ControlKind kind) => kind == ControlKind.CheckBox
        ? LocKey.Common.Checked
        : LocKey.Common.Enabled;

    public static LocKey OffLabel(ControlKind kind) => kind == ControlKind.CheckBox
        ? LocKey.Common.Unchecked
        : LocKey.Common.Disabled;

    public static LocKey Label(ControlKind kind, bool on) => on ? OnLabel(kind) : OffLabel(kind);

    public static bool Matches(IReadOnlyList<SettingState> states, ControlKind kind) =>
        states.Count == 2 && states.Any(s => s.Label == OnLabel(kind)) && states.Any(s => s.Label == OffLabel(kind));

    public static bool? GetRecommended(Setting setting, WinBuild build) => ForRole(setting, RoleKind.Recommended, build);

    public static bool? GetDefault(Setting setting, WinBuild build) => ForRole(setting, RoleKind.WindowsDefault, build);

    private static bool? ForRole(Setting setting, RoleKind role, WinBuild build)
    {
        var on = setting.States.FirstOrDefault(s => s.Label == OnLabel(setting.Control));
        if (on is not null && on.HasRole(role, build)) return true;
        var off = setting.States.FirstOrDefault(s => s.Label == OffLabel(setting.Control));
        if (off is not null && off.HasRole(role, build)) return false;
        return null;
    }
}
