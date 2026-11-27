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
        new PropertyMetadata(SettingInputMode.Single));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static double ValueFor(SettingItemViewModel? setting, SettingInputMode mode, int _1, int _2, int _3)
        => setting?.NumericValueForMode(mode) ?? 0d;

    public static double AsDouble(int value) => value;

    // Only Undetermined covers a numeric - any number the user types is legitimate, so there is no "unrecognized
    // value" here. Undetermined is setting-wide, so the setting's own outcome decides it for every mode.
    public static bool IsUndetermined(SettingDetectionOutcome outcome) => outcome == SettingDetectionOutcome.Undetermined;

    // On the covering Border, which is what the pointer hits.
    public static string? OutcomeTooltip(SettingItemViewModel? setting, SettingInputMode mode, SettingDetectionOutcome outcome)
        => IsUndetermined(outcome) ? setting?.OverlayTooltipForMode(mode, isTwoState: false) : null;

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
