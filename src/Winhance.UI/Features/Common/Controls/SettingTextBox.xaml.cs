using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingTextBox : UserControl, INotifyPropertyChanged
{
    public SettingTextBox()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingTextBox),
        new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public string TextValue { get; private set; } = string.Empty;
    public string TextError { get; private set; } = string.Empty;
    public Visibility ErrorVisibility { get; private set; } = Visibility.Collapsed;
    public string SettingName { get; private set; } = string.Empty;
    public string Placeholder { get; private set; } = string.Empty;
    public string BrowseLabel { get; private set; } = string.Empty;
    public Visibility BrowseVisibility { get; private set; } = Visibility.Collapsed;
    public bool IsPickerOnly { get; private set; }

    private SettingItemViewModel? _observed;

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingTextBox)d;
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
            or nameof(SettingItemViewModel.TextValue)
            or nameof(SettingItemViewModel.HasTextError)
            or nameof(SettingItemViewModel.Name))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (Setting is not { } vm)
            return;

        TextValue = vm.TextValue;
        TextError = vm.TextError;
        ErrorVisibility = vm.HasTextError ? Visibility.Visible : Visibility.Collapsed;
        SettingName = vm.Name;
        Placeholder = vm.TextPlaceholder;
        BrowseLabel = vm.BrowseLabel;
        BrowseVisibility = vm.HasTextBoxPicker ? Visibility.Visible : Visibility.Collapsed;
        IsPickerOnly = vm.HasTextBoxPicker;

        Notify(nameof(TextValue), nameof(TextError), nameof(ErrorVisibility), nameof(SettingName),
            nameof(Placeholder), nameof(BrowseLabel), nameof(BrowseVisibility), nameof(IsPickerOnly));
    }

    // The view model's equality guard swallows the echo a programmatic Refresh raises through the box.
    private void OnTextChanged(object sender, TextChangedEventArgs e)
        => Setting?.OnTextBoxChanged(((TextBox)sender).Text);

    private void OnBrowseClick(object sender, RoutedEventArgs e) => Setting?.BrowseForText();

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
