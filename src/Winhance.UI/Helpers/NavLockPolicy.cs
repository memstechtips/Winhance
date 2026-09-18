using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Helpers;

internal static class NavLockPolicy
{
    public static bool IsAutounattendLocked(WinhanceMode mode, BuilderTarget target) =>
        !(mode == WinhanceMode.Builder && target == BuilderTarget.Autounattend);

    public static bool IsWimUtilLocked(WinhanceMode mode) => mode == WinhanceMode.ConfigReview;

    // A locked button keeps its tab stop so a keyboard user can hear the reason, but UIA must not see it as pressable.
    public static bool IsInvokableByAutomation(bool isLocked) => !isLocked;

    public static string HelpTextFor(bool isLocked, string? tooltip) =>
        isLocked ? tooltip ?? string.Empty : string.Empty;

    // ModeChanged fires on every Builder target switch and re-applies every lock, so only the edge is announced.
    public static bool ShouldAnnounceUnlock(bool wasLocked, bool isLocked) => wasLocked && !isLocked;

    // The format comes from 29 translated files, so a broken one must not take the sidebar down on a mode change.
    public static string UnlockAnnouncement(string format, string buttonText)
    {
        try
        {
            return string.Format(format, buttonText);
        }
        catch (FormatException)
        {
            return buttonText;
        }
    }
}
