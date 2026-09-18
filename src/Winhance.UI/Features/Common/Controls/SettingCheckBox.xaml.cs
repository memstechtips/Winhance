using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingCheckBox : UserControl, INotifyPropertyChanged
{
    public SettingCheckBox()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingCheckBox),
        new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public bool? IsChecked { get; private set; }
    public string SettingName { get; private set; } = string.Empty;

    public double BoxOpacity { get; private set; } = 1d;
    public bool BoxInteractive { get; private set; } = true;
    public AccessibilityView BoxAccessibilityView { get; private set; } = AccessibilityView.Content;

    private SettingItemViewModel? _observed;

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingCheckBox)d;
        control.Detach();
        control._observed = e.NewValue as SettingItemViewModel;
        if (control._observed is { } vm)
            vm.PropertyChanged += control.OnSettingPropertyChanged;
        control.Refresh();
    }

    private void Detach()
    {
        if (_observed is { } vm)
            vm.PropertyChanged -= OnSettingPropertyChanged;
        _observed = null;
    }

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(SettingItemViewModel.IsSelected)
            or nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.Name))
        {
            Refresh();
        }
    }

    private void Refresh()
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

    // x:Bind OneWay subscribes here. Without it the compiler emits WMC1506 and the bindings refresh only when
    // something calls Bindings.Update() by hand, which fails silently when forgotten.

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(params string[] names)
    {
        var handler = PropertyChanged;
        if (handler is null)
            return;
        foreach (var name in names)
            handler(this, new PropertyChangedEventArgs(name));
    }
}
