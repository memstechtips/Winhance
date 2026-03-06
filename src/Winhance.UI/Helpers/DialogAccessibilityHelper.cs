using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Winhance.UI.Helpers;

/// <summary>
/// Shared accessibility helper for announcing UI state changes to Narrator.
/// Replaces the duplicate local <c>Announce</c> / <c>AnnounceCheckbox</c> functions
/// that were scattered across DialogService methods.
/// </summary>
internal static class DialogAccessibilityHelper
{
    /// <summary>
    /// Announces a message to Narrator via the automation peer of the given element.
    /// </summary>
    /// <param name="element">The UI element whose automation peer will raise the notification.</param>
    /// <param name="announcement">The text to announce.</param>
    /// <param name="activityId">An identifier for the activity (for grouping/replacing notifications).</param>
    public static void AnnounceToNarrator(UIElement element, string announcement, string activityId = "DialogNotification")
    {
        var peer = FrameworkElementAutomationPeer.FromElement(element)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            announcement,
            activityId);
    }
}
