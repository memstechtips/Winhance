using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingNumberBox : UserControl, INotifyPropertyChanged
{
    public SettingNumberBox()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingNumberBox),
        new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingNumberBox),
        new PropertyMetadata(SettingInputMode.Single, OnAnyChanged));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    // --- Projected, bindable ---------------------------------------------------------------------

    public double NumericValue { get; private set; }
    public double Minimum { get; private set; }
    public double Maximum { get; private set; }
    public string InputAutomationName { get; private set; } = string.Empty;

    // Only Undetermined covers a numeric - any number the user types is legitimate, so there is no "unrecognized value" here.
    public Visibility OverlayVisibility { get; private set; } = Visibility.Collapsed;

    // On the covering Border, which is what the pointer hits.
    public string? OutcomeTooltip { get; private set; }

    private SettingItemViewModel? _observed;

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingNumberBox)d;
        control.Detach();
        control._observed = e.NewValue as SettingItemViewModel;
        if (control._observed is { } vm)
            vm.PropertyChanged += control.OnSettingPropertyChanged;
        control.Refresh();
    }

    private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SettingNumberBox)d).Refresh();

    private void Detach()
    {
        if (_observed is { } vm)
            vm.PropertyChanged -= OnSettingPropertyChanged;
        _observed = null;
    }

    private void OnSettingPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(SettingItemViewModel.NumericValue)
            or nameof(SettingItemViewModel.AcNumericValue)
            or nameof(SettingItemViewModel.DcNumericValue)
            or nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.MinValue)
            or nameof(SettingItemViewModel.MaxValue)
            or nameof(SettingItemViewModel.Name))
        {
            Refresh();
        }
    }

    private void Refresh()
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
            ? vm.OverlayTooltipForMode(Mode, toggleLike: false)
            : null;
        Notify(nameof(NumericValue), nameof(Minimum), nameof(Maximum),
               nameof(InputAutomationName), nameof(OverlayVisibility), nameof(OutcomeTooltip));
    }

    // --- INotifyPropertyChanged -------------------------------------------------------------------
    // x:Bind OneWay subscribes here. Without it the compiler emits WMC1506 ("OneWay bindings require
    // at least one of their steps to support raising notifications") and the bindings only refresh
    // because something calls Bindings.Update() by hand - which is easy to forget when adding a
    // property, and fails silently when you do.

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(params string[] names)
    {
        var handler = PropertyChanged;
        if (handler is null)
            return;
        foreach (var name in names)
            handler(this, new PropertyChangedEventArgs(name));
    }


    // --- Event routing ---------------------------------------------------------------------------

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
