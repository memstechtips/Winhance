using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using VirtualKey = Windows.System.VirtualKey;

namespace Winhance.UI.Features.Common.Controls;

// Events are routed here rather than bound in markup because the handler differs per mode
// (OnComboBoxDropDownClosed / OnAC... / OnDC...) and x:Bind cannot pick one at runtime.
public sealed partial class SettingComboBox : SettingBoundControl
{
    // The dot's colour is the status itself, so it does not follow the theme.
    private static readonly SolidColorBrush ExistsBrush = new(Color.FromArgb(255, 0, 200, 60));
    private static readonly SolidColorBrush NotExistsBrush = new(Color.FromArgb(255, 200, 40, 0));

    private readonly IDispatcherService _dispatcherService = App.Services.GetRequiredService<IDispatcherService>();
    private ObservableCollection<ComboBoxDisplayOption>? _options;
    private bool _resyncQueued;

    public event EventHandler<DynamicOption>? DeleteRequested;

    public SettingComboBox()
    {
        InitializeComponent();
        Input.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnItemsSourceChanged);
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingComboBox),
        new PropertyMetadata(SettingInputMode.Single));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    // The AC/DC layout pins 120 because two columns otherwise grow to their widest item and squeeze the badges out
    // of the row; everywhere else NaN (auto). A lone Width does not survive the remeasure after the popup closes,
    // which is why Width, MinWidth and MaxWidth are all driven from it.
    public static readonly DependencyProperty PinnedWidthProperty = DependencyProperty.Register(
        nameof(PinnedWidth), typeof(double), typeof(SettingComboBox),
        new PropertyMetadata(double.NaN));

    public double PinnedWidth
    {
        get => (double)GetValue(PinnedWidthProperty);
        set => SetValue(PinnedWidthProperty, value);
    }

    // The status dot, the Active badge and the delete button sit on the dropdown rows only, which the closed box does
    // not measure, so those dropdowns get a floor.
    private const double OptionStatusMinWidth = 220d;

    public static double MinWidthFor(double pin, bool showsOptionStatus) =>
        Math.Max(MinWidthForPin(pin), showsOptionStatus ? OptionStatusMinWidth : 0d);

    // Width alone accepts NaN as "auto"; MinWidth and MaxWidth reject it with E_INVALIDARG - an unguarded MinWidth
    // threw on every unpinned dropdown. Static so the guards are testable without a XAML application.
    public static double MinWidthForPin(double pin) => double.IsNaN(pin) ? 0d : pin;

    public static double MaxWidthForPin(double pin) => double.IsNaN(pin) ? double.PositiveInfinity : pin;

    // The trailing arguments are what the index is read from: the per-mode values, and the option list, whose
    // position decides a keyed selection's index. x:Bind calls the function again when any of them changes.
    public static int IndexFor(SettingItemViewModel? setting, SettingInputMode mode,
        object? _1, int _2, int _3, ObservableCollection<ComboBoxDisplayOption> _4)
        => setting?.ComboIndexForMode(mode) ?? ComboBoxConstants.CustomStateIndex;

    // On the CONTROL, not the overlay - the overlay passes pointer input through, so a tooltip on it could never
    // fire. Null while resolved so no empty tooltip appears.
    public static string? OutcomeTooltip(SettingItemViewModel? setting, SettingInputMode mode,
        SettingDetectionOutcome _1, object? _2, int _3, int _4)
        => setting is null || setting.OutcomeForMode(mode) == SettingDetectionOutcome.Resolved
            ? null
            : setting.OverlayTooltipForMode(mode, isTwoState: false);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Announce-only (screen-reader notification); the view model ignores programmatic changes.
        Setting?.OnComboBoxSelectionChanged(sender, e);
    }

    private void ObserveOptions(ObservableCollection<ComboBoxDisplayOption>? options)
    {
        if (ReferenceEquals(_options, options))
            return;

        if (_options is { } old)
            old.CollectionChanged -= OnOptionsChanged;
        _options = options;
        if (_options is { } current)
            current.CollectionChanged += OnOptionsChanged;
    }

    private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty property)
    {
        if (Input.IsLoaded)
            ObserveOptions(Input.ItemsSource as ObservableCollection<ComboBoxDisplayOption>);
    }

    // Replacing the selected item resets the ComboBox's SelectedIndex to -1, and a rebuild onto the same key changes
    // no view-model property, so the index is pushed again here. Queued, so it lands after the whole rebuild and after
    // the ComboBox's own handling of each change; an unloaded control has stopped observing and skips it.
    private void OnOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_resyncQueued)
            return;

        _resyncQueued = true;
        _dispatcherService.Enqueue(() =>
        {
            _resyncQueued = false;
            if (_options is not null)
                PushIndex();
        });
    }

    // A list that changed while unloaded went unobserved, so the index is pushed again on the way back in.
    private void OnInputLoaded(object sender, RoutedEventArgs e)
    {
        ObserveOptions(Input.ItemsSource as ObservableCollection<ComboBoxDisplayOption>);
        PushIndex();
    }

    private void OnInputUnloaded(object sender, RoutedEventArgs e) => ObserveOptions(null);

    // A local value set over a OneWay x:Bind target leaves the binding in place; its next update overwrites it.
    private void PushIndex()
    {
        if (Setting is { } vm)
            Input.SelectedIndex = vm.ComboIndexForMode(Mode);
    }

    public static SolidColorBrush StatusBrush(bool existsOnSystem) => existsOnSystem ? ExistsBrush : NotExistsBrush;

    // Tab dismisses an open ComboBox, so Delete on the focused row is the only keyboard route to the button.
    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Delete
            && e.OriginalSource is ComboBoxItem { Content: ComboBoxDisplayOption { CanDelete: true, Tag: DynamicOption option } })
        {
            DeleteRequested?.Invoke(this, option);
            e.Handled = true;
        }
    }

    private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ComboBoxDisplayOption { Tag: DynamicOption option } })
            DeleteRequested?.Invoke(this, option);
    }

    private void OnDropDownClosed(object sender, object e)
    {
        if (Setting is not { } vm)
            return;

        switch (Mode)
        {
            case SettingInputMode.Ac:
                vm.OnACComboBoxDropDownClosed(sender);
                break;
            case SettingInputMode.Dc:
                vm.OnDCComboBoxDropDownClosed(sender);
                break;
            default:
                vm.OnComboBoxDropDownClosed(sender);
                break;
        }
    }
}
