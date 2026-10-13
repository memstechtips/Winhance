namespace Winhance.Core.Features.SoftwareApps.Utilities;

// Once Game Bar is gone, games that start with Game DVR on bring up a Store prompt for the missing overlay, and
// turning Game DVR off is the fix. Removing any of these packages turns it off once, through its setting, so the
// card shows the change and the user can turn it back on.
public static class GameBarRemoval
{
    public const string GameDvrSettingId = "gaming-xbox-game-dvr";

    public static readonly string[] Packages = ["Microsoft.GamingApp", "Microsoft.XboxGamingOverlay", "Microsoft.XboxGameOverlay"];

    public static bool Includes(IEnumerable<string>? packageNames) =>
        packageNames?.Any(name => Packages.Contains(name, StringComparer.OrdinalIgnoreCase)) == true;
}
