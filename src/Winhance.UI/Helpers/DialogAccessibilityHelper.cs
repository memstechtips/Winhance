using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Helpers;

internal static class DialogAccessibilityHelper
{
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

    public static CheckBox AnnouncedCheckBox(string text, bool isChecked, ILocalizationService localization)
    {
        var checkBox = new CheckBox { Content = text, IsChecked = isChecked };
        checkBox.Checked += (_, _) => AnnounceToNarrator(
            checkBox, $"{text}: {localization.GetStringOrDefault("Common_Checked", "Checked")}", "CheckboxStateChange");
        checkBox.Unchecked += (_, _) => AnnounceToNarrator(
            checkBox, $"{text}: {localization.GetStringOrDefault("Common_Unchecked", "Unchecked")}", "CheckboxStateChange");
        return checkBox;
    }
}
