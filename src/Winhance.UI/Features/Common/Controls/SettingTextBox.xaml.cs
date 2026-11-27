using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingTextBox : SettingBoundControl
{
    public SettingTextBox()
    {
        InitializeComponent();
    }

    // The view model's equality guard swallows the echo the OneWay binding raises through the box.
    private void OnTextChanged(object sender, TextChangedEventArgs e)
        => Setting?.OnTextBoxChanged(((TextBox)sender).Text);

    private void OnBrowseClick(object sender, RoutedEventArgs e) => Setting?.BrowseForText();
}
