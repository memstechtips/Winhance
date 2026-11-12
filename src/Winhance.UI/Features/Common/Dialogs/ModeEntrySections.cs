using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Helpers;

namespace Winhance.UI.Features.Common.Dialogs;

// The reason, discard warning and mode explanation that Builder's and Config Review's entry dialogs show above their
// choices, so entering a mode is one dialog rather than a chain of them.
internal sealed class ModeEntrySections(ILocalizationService localization, ModeEntryNotes notes)
{
    private CheckBox? _dontShowAgain;

    // Read after ShowAsync returns.
    public bool HideExplanation => _dontShowAgain?.IsChecked == true;

    // Null when there is nothing to say.
    public StackPanel? Build()
    {
        var panel = new StackPanel { Spacing = 12 };

        if (!string.IsNullOrEmpty(notes.Reason))
        {
            panel.Children.Add(new TextBlock
            {
                Text = notes.Reason,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold
            });
        }

        if (!string.IsNullOrEmpty(notes.Warning))
        {
            panel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Message = notes.Warning,
            });
        }

        if (!string.IsNullOrEmpty(notes.Explanation))
        {
            panel.Children.Add(new TextBlock { Text = notes.Explanation, TextWrapping = TextWrapping.Wrap });
            _dontShowAgain = DialogAccessibilityHelper.AnnouncedCheckBox(
                localization.GetString("Dialog_Mode_DontShowAgain"), isChecked: false, localization);
            panel.Children.Add(_dontShowAgain);
        }

        return panel.Children.Count > 0 ? panel : null;
    }
}
