using Microsoft.UI.Windowing;

namespace Winhance.UI.Features.Common.Utilities;

// A WinUI window has no icon of its own, so without this Task Manager shows a blank window under Winhance.
internal static class WindowIcon
{
    public static void Apply(AppWindow window) =>
        window.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcons", "winhance-rocket.ico"));
}
