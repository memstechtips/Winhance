using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingCheckBox : SettingBoundControl
{
    public SettingCheckBox()
    {
        InitializeComponent();
    }

    // The view model's equality guard swallows the echo the OneWay binding raises.
    private void OnChanged(object sender, RoutedEventArgs e)
        => Setting?.OnCheckBoxChanged(((CheckBox)sender).IsChecked == true);
}
