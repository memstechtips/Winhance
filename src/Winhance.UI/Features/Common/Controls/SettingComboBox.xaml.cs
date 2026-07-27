using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Code-behind for <see cref="SettingComboBox"/>. Projects the view model's per-mode resolution into
/// bindable properties and routes the ComboBox's events to the right view-model handler for this mode.
///
/// The events are routed here rather than bound in markup because the handler differs per mode
/// (OnComboBoxDropDownClosed / OnACComboBoxDropDownClosed / OnDCComboBoxDropDownClosed) and x:Bind cannot
/// pick one at runtime. Each of those reads what it needs off the sender, so passing the real ComboBox
/// through keeps their existing behaviour exactly.
/// </summary>
public sealed partial class SettingComboBox : UserControl
{
    public SettingComboBox()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingComboBox),
        new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingComboBox),
        new PropertyMetadata(SettingInputMode.Single, OnAnyChanged));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Pins the closed-state width. The AC/DC layout sets 120 because two columns otherwise grow
    /// to their widest item and squeeze the badges out of the row; everywhere else this stays NaN (auto).
    /// A lone Width does not survive the remeasure after the popup closes, which is why Width, MinWidth
    /// and MaxWidth are all driven from it.</summary>
    public static readonly DependencyProperty PinnedWidthProperty = DependencyProperty.Register(
        nameof(PinnedWidth), typeof(double), typeof(SettingComboBox),
        new PropertyMetadata(double.NaN, OnAnyChanged));

    public double PinnedWidth
    {
        get => (double)GetValue(PinnedWidthProperty);
        set => SetValue(PinnedWidthProperty, value);
    }

    /// <summary>MaxWidth mirrors the pin, but must be PositiveInfinity (not NaN) when unpinned.</summary>
    public double PinnedMaxWidth => double.IsNaN(PinnedWidth) ? double.PositiveInfinity : PinnedWidth;

    // --- Projected, bindable ---------------------------------------------------------------------

    public ObservableCollection<ComboBoxDisplayOption>? Options { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    public string InputAutomationName { get; private set; } = string.Empty;

    private SettingItemViewModel? _observed;

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingComboBox)d;
        control.Detach();
        control._observed = e.NewValue as SettingItemViewModel;
        if (control._observed is { } vm)
            vm.PropertyChanged += control.OnSettingPropertyChanged;
        control.Refresh();
    }

    private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SettingComboBox)d).Refresh();

    private void Detach()
    {
        if (_observed is { } vm)
            vm.PropertyChanged -= OnSettingPropertyChanged;
        _observed = null;
    }

    private void OnSettingPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(SettingItemViewModel.SelectedValue)
            or nameof(SettingItemViewModel.AcValue)
            or nameof(SettingItemViewModel.DcValue)
            or nameof(SettingItemViewModel.ComboBoxOptions)
            or nameof(SettingItemViewModel.Name))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (Setting is not { } vm)
            return;

        Options = vm.ComboBoxOptions;
        SelectedIndex = vm.ComboIndexForMode(Mode);
        InputAutomationName = vm.InputAutomationNameForMode(Mode);
        Bindings.Update();
    }

    // --- Event routing ---------------------------------------------------------------------------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Announce-only (screen-reader notification); the view model ignores programmatic changes.
        Setting?.OnComboBoxSelectionChanged(sender, e);
    }

    private void OnDropDownClosed(object sender, object e)
    {
        if (Setting is not { } vm)
            return;

        switch (Mode)
        {
            case SettingInputMode.Ac:
                vm.OnACComboBoxDropDownClosed(sender, e);
                break;
            case SettingInputMode.Dc:
                vm.OnDCComboBoxDropDownClosed(sender, e);
                break;
            default:
                vm.OnComboBoxDropDownClosed(sender, e);
                break;
        }
    }
}
