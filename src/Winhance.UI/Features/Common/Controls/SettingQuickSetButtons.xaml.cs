using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>Which family of quick-set commands this pair drives. The commands differ per control type as
/// well as per power mode, and both axes are resolved on the view model.</summary>
public enum SettingQuickSetKind
{
    Toggle,
    Selection,
    Numeric,
}

/// <summary>
/// Code-behind for <see cref="SettingQuickSetButtons"/>. Resolves (kind x mode) into the concrete command,
/// tooltip and automation name through the view model, so the markup stays one declaration.
/// </summary>
public sealed partial class SettingQuickSetButtons : UserControl, INotifyPropertyChanged
{
    public SettingQuickSetButtons()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingQuickSetButtons),
        new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingQuickSetButtons),
        new PropertyMetadata(SettingInputMode.Single, OnAnyChanged));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(SettingQuickSetKind), typeof(SettingQuickSetButtons),
        new PropertyMetadata(SettingQuickSetKind.Selection, OnAnyChanged));

    public SettingQuickSetKind Kind
    {
        get => (SettingQuickSetKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    // --- Projected, bindable ---------------------------------------------------------------------

    public Visibility ButtonsVisibility { get; private set; } = Visibility.Collapsed;
    public IRelayCommand? RecommendedCommand { get; private set; }
    public IRelayCommand? DefaultCommand { get; private set; }
    public string? RecommendedTooltip { get; private set; }
    public string? DefaultTooltip { get; private set; }
    public string RecommendedAutomationName { get; private set; } = string.Empty;
    public string DefaultAutomationName { get; private set; } = string.Empty;

    private SettingItemViewModel? _observed;

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingQuickSetButtons)d;
        control.Detach();
        control._observed = e.NewValue as SettingItemViewModel;
        if (control._observed is { } vm)
            vm.PropertyChanged += control.OnSettingPropertyChanged;
        control.Refresh();
    }

    private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SettingQuickSetButtons)d).Refresh();

    private void Detach()
    {
        if (_observed is { } vm)
            vm.PropertyChanged -= OnSettingPropertyChanged;
        _observed = null;
    }

    // The visibility flags and tooltips change as the setting's value and language change; a null
    // PropertyName means "everything", which the view model raises on a language switch.
    private void OnSettingPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Refresh();

    private void Refresh()
    {
        if (Setting is not { } vm)
        {
            ButtonsVisibility = Visibility.Collapsed;
            NotifyAll();
            return;
        }

        bool show;
        switch (Kind)
        {
            case SettingQuickSetKind.Toggle:
                show = vm.ShowToggleQuickSetButtons;
                RecommendedCommand = vm.SetToggleToRecommendedCommand;
                DefaultCommand = vm.SetToggleToDefaultCommand;
                RecommendedTooltip = vm.ToggleRecommendedTooltip;
                DefaultTooltip = vm.ToggleDefaultTooltip;
                break;

            case SettingQuickSetKind.Numeric:
                show = vm.ShowNumericQuickSetButtons;
                RecommendedCommand = vm.NumericRecommendedCommandForMode(Mode);
                DefaultCommand = vm.NumericDefaultCommandForMode(Mode);
                RecommendedTooltip = vm.NumericRecommendedTooltipForMode(Mode);
                DefaultTooltip = vm.NumericDefaultTooltipForMode(Mode);
                break;

            default:
                show = vm.ShowSelectionQuickSetForMode(Mode);
                RecommendedCommand = vm.SelectionRecommendedCommandForMode(Mode);
                DefaultCommand = vm.SelectionDefaultCommandForMode(Mode);
                RecommendedTooltip = vm.SelectionRecommendedTooltipForMode(Mode);
                DefaultTooltip = vm.SelectionDefaultTooltipForMode(Mode);
                break;
        }

        ButtonsVisibility = show ? Visibility.Visible : Visibility.Collapsed;
        RecommendedAutomationName = vm.A11yNameForMode(Mode, RecommendedTooltip);
        DefaultAutomationName = vm.A11yNameForMode(Mode, DefaultTooltip);
        NotifyAll();
    }

    private void NotifyAll() => Notify(
        nameof(ButtonsVisibility), nameof(RecommendedCommand), nameof(DefaultCommand),
        nameof(RecommendedTooltip), nameof(DefaultTooltip),
        nameof(RecommendedAutomationName), nameof(DefaultAutomationName));

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

}
