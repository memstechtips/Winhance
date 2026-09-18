using Windows.System;

namespace Winhance.UI.Helpers;

// Ctrl+1..Ctrl+6 in sidebar order; MainWindow.xaml declares one KeyboardAccelerator per entry and must match.
internal static class NavAccelerators
{
    public static readonly IReadOnlyList<string> Tags =
        ["SoftwareApps", "Optimize", "Customize", "Autounattend", "WimUtil", "Settings"];

    public static string? TagFor(VirtualKey key)
    {
        var index = (int)key - (int)VirtualKey.Number1;
        return index >= 0 && index < Tags.Count ? Tags[index] : null;
    }
}
