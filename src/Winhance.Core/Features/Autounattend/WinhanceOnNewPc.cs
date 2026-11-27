using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.Autounattend;

// Winhance is what installs the apps on the new PC, so a file with apps always installs Winhance after setup. A
// desktop shortcut would leave them waiting for a double-click, and Don't add would drop them. Keeping removed
// apps removed with Winhance needs it on the new PC for the same reason.
public static class WinhanceOnNewPc
{
    public const string SettingId = "autounattend-desktop-shortcut";

    public const string KeepRemovedId = "autounattend-keep-removed";

    public static LocKey InstallsAutomatically => LocKey.Setting.AutounattendDesktopShortcut.Option2;

    public static bool InstallsApps(SelectionSet set) =>
        set.ExternalApps.Count > 0 || set.Value(OwnInstaller.SettingId) is ChoiceValue.List { Rows.Count: > 0 };

    // Nothing to keep removed without removed apps, so an empty list does not pull Winhance in.
    public static bool KeepsRemovedAppsWithWinhance(SelectionSet set) =>
        set.WindowsApps.Count > 0 && set.StateOf(KeepRemovedId)?.Label == LocKey.Setting.AutounattendKeepRemoved.Option2;

    public static SelectionSet Enforce(SelectionSet set)
    {
        if (!(InstallsApps(set) || KeepsRemovedAppsWithWinhance(set)) || SettingCatalog.Find(SettingId) is not { } setting)
            return set;

        var index = setting.States.ToList().FindIndex(state => state.Label == InstallsAutomatically);
        var choice = new SettingChoice(SettingId, new ChoiceValue.Option(index));
        return set with
        {
            Settings = [.. set.Settings.Where(c => SettingIdAliases.Normalize(c.SettingId) != SettingId), choice],
        };
    }
}
