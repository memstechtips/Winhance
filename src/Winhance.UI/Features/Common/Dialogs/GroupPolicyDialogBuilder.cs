using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Dialogs;

// The preview copies what Windows Settings shows for a policy: its banner, then a greyed card beside a normal one.
internal static class GroupPolicyDialogBuilder
{
    public static UIElement BuildContent(ILocalizationService localization)
    {
        var managedMessage = localization.GetString("GroupPolicy_ManagedMessage");
        var panel = new StackPanel { Spacing = 12, MaxWidth = 520 };
        panel.Children.Add(Paragraph(localization.GetString("GroupPolicy_Explanation", managedMessage)));
        panel.Children.Add(Paragraph(localization.GetString("GroupPolicy_Dialog_Preview")));
        panel.Children.Add(new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Informational,
            Message = managedMessage,
        });
        panel.Children.Add(PreviewCard(localization, "GroupPolicy_Dialog_Managed", isEnabled: false));
        panel.Children.Add(PreviewCard(localization, "GroupPolicy_Dialog_Free", isEnabled: true));
        panel.Children.Add(Paragraph(localization.GetString("GroupPolicy_Dialog_BuiltIn")));
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static TextBlock Paragraph(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    private static SettingsCard PreviewCard(ILocalizationService localization, string keyPrefix, bool isEnabled) => new()
    {
        Header = localization.GetString(keyPrefix + "_Name"),
        Description = localization.GetString(keyPrefix + "_Description"),
        IsEnabled = isEnabled,
        Content = new ToggleSwitch
        {
            OnContent = localization.GetString("Common_On"),
            OffContent = localization.GetString("Common_Off"),
        },
    };
}
