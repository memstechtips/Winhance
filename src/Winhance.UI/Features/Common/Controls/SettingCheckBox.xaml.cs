using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingCheckBox : SettingBoundControl
{
    public SettingCheckBox()
    {
        InitializeComponent();
    }

    public bool? IsChecked { get; private set; }
    public string SettingName { get; private set; } = string.Empty;

    public double BoxOpacity { get; private set; } = 1d;
    public bool BoxInteractive { get; private set; } = true;
    public AccessibilityView BoxAccessibilityView { get; private set; } = AccessibilityView.Content;

    protected override bool RefreshesOn(string propertyName)
        => propertyName is nameof(SettingItemViewModel.IsSelected)
            or nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.Name);

    protected override void Refresh()
    {
        if (Setting is not { } vm)
            return;

        IsChecked = vm.IsSelected;
        SettingName = vm.Name;

        BoxOpacity = vm.TwoStateOpacityFor(vm.Outcome);
        BoxInteractive = vm.TwoStateInteractiveFor(vm.Outcome);
        BoxAccessibilityView = vm.TwoStateAccessibilityViewFor(vm.Outcome);

        Notify(
            nameof(IsChecked), nameof(SettingName),
            nameof(BoxOpacity), nameof(BoxInteractive), nameof(BoxAccessibilityView));
    }

    // The view model's equality guard swallows the echo a programmatic Refresh raises.
    private void OnChanged(object sender, RoutedEventArgs e)
        => Setting?.OnCheckBoxChanged(((CheckBox)sender).IsChecked == true);
}
