using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingNumberBox : SettingBoundControl
{
    public SettingNumberBox()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingNumberBox),
        new PropertyMetadata(SettingInputMode.Single, RefreshOnChange));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double NumericValue { get; private set; }
    public double Minimum { get; private set; }
    public double Maximum { get; private set; }
    public string InputAutomationName { get; private set; } = string.Empty;

    // Only Undetermined covers a numeric - any number the user types is legitimate, so there is no "unrecognized value" here.
    public Visibility OverlayVisibility { get; private set; } = Visibility.Collapsed;

    // On the covering Border, which is what the pointer hits.
    public string? OutcomeTooltip { get; private set; }

    protected override bool RefreshesOn(string propertyName)
        => propertyName is nameof(SettingItemViewModel.NumericValue)
            or nameof(SettingItemViewModel.AcNumericValue)
            or nameof(SettingItemViewModel.DcNumericValue)
            or nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.MinValue)
            or nameof(SettingItemViewModel.MaxValue)
            or nameof(SettingItemViewModel.Name);

    protected override void Refresh()
    {
        if (Setting is not { } vm)
            return;

        NumericValue = vm.NumericValueForMode(Mode);
        Minimum = vm.MinValue;
        Maximum = vm.MaxValue;
        InputAutomationName = vm.InputAutomationNameForMode(Mode);
        OverlayVisibility = vm.OutcomeForMode(Mode) == SettingDetectionOutcome.Undetermined
            ? Visibility.Visible
            : Visibility.Collapsed;
        OutcomeTooltip = OverlayVisibility == Visibility.Visible
            ? vm.OverlayTooltipForMode(Mode, isTwoState: false)
            : null;
        Notify(nameof(NumericValue), nameof(Minimum), nameof(Maximum),
               nameof(InputAutomationName), nameof(OverlayVisibility), nameof(OutcomeTooltip));
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Setting?.OnNumberBoxLoaded(sender);

    private void OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (Setting is not { } vm)
            return;

        switch (Mode)
        {
            case SettingInputMode.Ac:
                vm.OnACNumberBoxValueChanged(e);
                break;
            case SettingInputMode.Dc:
                vm.OnDCNumberBoxValueChanged(e);
                break;
            default:
                vm.OnNumberBoxValueChanged(e);
                break;
        }
    }
}
