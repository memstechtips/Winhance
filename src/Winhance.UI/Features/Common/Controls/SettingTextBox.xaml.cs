using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingTextBox : SettingBoundControl
{
    public SettingTextBox()
    {
        InitializeComponent();
    }

    public string TextValue { get; private set; } = string.Empty;
    public string TextError { get; private set; } = string.Empty;
    public Visibility ErrorVisibility { get; private set; } = Visibility.Collapsed;
    public string SettingName { get; private set; } = string.Empty;
    public string Placeholder { get; private set; } = string.Empty;
    public string BrowseLabel { get; private set; } = string.Empty;
    public Visibility BrowseVisibility { get; private set; } = Visibility.Collapsed;
    public bool IsPickerOnly { get; private set; }

    protected override bool RefreshesOn(string propertyName)
        => propertyName is nameof(SettingItemViewModel.TextValue)
            or nameof(SettingItemViewModel.HasTextError)
            or nameof(SettingItemViewModel.Name);

    protected override void Refresh()
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
}
