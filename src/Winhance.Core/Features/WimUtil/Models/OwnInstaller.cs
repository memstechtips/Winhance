using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.WimUtil.Models;

public sealed record OwnInstaller(string FilePath, string SilentSwitches)
{
    public const string SettingId = "autounattend-own-installers";
    public const string StageAppsId = "autounattend-stage-apps";

    // The set may come from a file anyone wrote, so only a full path to an installer is believed. Don't add leaves
    // nothing on the new PC to run them.
    public static IReadOnlyList<OwnInstaller> From(SelectionSet set)
    {
        if (set.StateOf("autounattend-desktop-shortcut")?.Label == LocKey.Setting.AutounattendDesktopShortcut.Option0
            || set.Value(SettingId) is not ChoiceValue.List list)
            return [];

        return [.. list.Rows
            .Select(row => new OwnInstaller(
                row.Values.GetValueOrDefault("file", string.Empty).Trim(),
                row.Values.GetValueOrDefault("switches", string.Empty).Trim()))
            .Where(IsInstaller)];
    }

    public static bool StagesApps(SelectionSet set) => set.StateOf(StageAppsId)?.Label == LocKey.Common.Checked;

    private static bool IsInstaller(OwnInstaller installer) =>
        installer.FilePath is { Length: > 0 } path
        && Path.IsPathFullyQualified(path)
        && Path.GetExtension(path) is { } extension
        && (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) || extension.Equals(".msi", StringComparison.OrdinalIgnoreCase));
}
