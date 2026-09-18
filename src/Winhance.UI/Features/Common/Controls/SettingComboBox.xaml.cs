using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using VirtualKey = Windows.System.VirtualKey;

namespace Winhance.UI.Features.Common.Controls;

// Events are routed here rather than bound in markup because the handler differs per mode
// (OnComboBoxDropDownClosed / OnAC... / OnDC...) and x:Bind cannot pick one at runtime.
public sealed partial class SettingComboBox : UserControl, INotifyPropertyChanged
{
    // The dot's colour is the status itself, so it does not follow the theme.
    private static readonly SolidColorBrush ExistsBrush = new(Color.FromArgb(255, 0, 200, 60));
    private static readonly SolidColorBrush NotExistsBrush = new(Color.FromArgb(255, 200, 40, 0));

    public event EventHandler<DynamicOption>? DeleteRequested;

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

    // The AC/DC layout pins 120 because two columns otherwise grow to their widest item and squeeze the badges out
    // of the row; everywhere else NaN (auto). A lone Width does not survive the remeasure after the popup closes,
    // which is why Width, MinWidth and MaxWidth are all driven from it.
    public static readonly DependencyProperty PinnedWidthProperty = DependencyProperty.Register(
        nameof(PinnedWidth), typeof(double), typeof(SettingComboBox),
        new PropertyMetadata(double.NaN, OnAnyChanged));

    public double PinnedWidth
    {
        get => (double)GetValue(PinnedWidthProperty);
        set => SetValue(PinnedWidthProperty, value);
    }

    public double PinnedMaxWidth => MaxWidthForPin(PinnedWidth);

    // The status dot and the delete button turn on after the popup has measured, so those dropdowns get a floor.
    private const double OptionStatusMinWidth = 220d;

    public double PinnedMinWidth =>
        Math.Max(MinWidthForPin(PinnedWidth), Setting is { ShowsOptionStatus: true } ? OptionStatusMinWidth : 0d);

    // Width alone accepts NaN as "auto"; MinWidth and MaxWidth reject it with E_INVALIDARG - an unguarded MinWidth
    // threw on every unpinned dropdown. Static so the guards are testable without a XAML application.
    public static double MinWidthForPin(double pin) => double.IsNaN(pin) ? 0d : pin;

    public static double MaxWidthForPin(double pin) => double.IsNaN(pin) ? double.PositiveInfinity : pin;

    public ObservableCollection<ComboBoxDisplayOption>? Options { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    public string InputAutomationName { get; private set; } = string.Empty;

    // On the CONTROL, not the overlay - the overlay passes pointer input through, so a tooltip on it could never
    // fire. Null while resolved so no empty tooltip appears.
    public string? OutcomeTooltip { get; private set; }

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
        OutcomeTooltip = vm.OutcomeForMode(Mode) == SettingDetectionOutcome.Resolved
            ? null
            : vm.OverlayTooltipForMode(Mode, isTwoState: false);
        // PinnedMinWidth/PinnedMaxWidth are derived from the PinnedWidth DP, so they are announced here
        // too - the DP's own change callback routes through Refresh.
        Notify(nameof(Options), nameof(SelectedIndex), nameof(InputAutomationName), nameof(OutcomeTooltip),
            nameof(PinnedMinWidth), nameof(PinnedMaxWidth));
    }

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

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Announce-only (screen-reader notification); the view model ignores programmatic changes.
        Setting?.OnComboBoxSelectionChanged(sender, e);
    }

    private void OnDropDownOpened(object sender, object e)
    {
        // Item containers exist only while the popup is up, so the per-item visuals are applied on every open.
        if (Setting is { ShowsOptionStatus: true } vm)
            DispatcherQueue.TryEnqueue(() => UpdateAllItemVisualStates(vm));
    }

    private void UpdateAllItemVisualStates(SettingItemViewModel vm)
    {
        for (int i = 0; i < vm.ComboBoxOptions.Count; i++)
        {
            if (Input.ContainerFromIndex(i) is not ComboBoxItem container
                || vm.ComboBoxOptions[i].Tag is not DynamicOption option
                || FindChild<Grid>(container, null) is not { } row)
                continue;

            if (FindChild<Ellipse>(row, "StatusIndicator") is { } status)
            {
                status.Visibility = Visibility.Visible;
                status.Fill = option.ExistsOnSystem ? ExistsBrush : NotExistsBrush;
                ToolTipService.SetToolTip(status,
                    option.ExistsOnSystem ? vm.KeyedInstalledTooltipText : vm.KeyedNotInstalledTooltipText);
            }

            if (FindChild<TextBlock>(row, "ActiveBadge") is { } badge)
            {
                badge.Text = vm.KeyedActiveBadgeText;
                badge.Visibility = string.Equals(option.Value, vm.LiveKeyedSelection, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (FindChild<Button>(row, "DeleteButton") is { } delete)
            {
                delete.Visibility = option.CanDelete ? Visibility.Visible : Visibility.Collapsed;
                ToolTipService.SetToolTip(delete, vm.KeyedDeleteTooltipText);
                AutomationProperties.SetName(delete, vm.KeyedDeleteTooltipText);
                delete.Tag = option;
                delete.Click -= OnDeleteButtonClick;
                delete.Click += OnDeleteButtonClick;
            }

            // Tab dismisses an open ComboBox, so Delete on the focused item is the only keyboard route to the button.
            container.Tag = option;
            container.KeyDown -= OnItemKeyDown;
            container.KeyDown += OnItemKeyDown;
        }
    }

    private void OnItemKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Delete
            && sender is ComboBoxItem { Tag: DynamicOption { CanDelete: true } option })
        {
            DeleteRequested?.Invoke(this, option);
            e.Handled = true;
        }
    }

    private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DynamicOption option })
            DeleteRequested?.Invoke(this, option);
    }

    private static T? FindChild<T>(DependencyObject parent, string? childName) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && (childName == null || typedChild.Name == childName))
                return typedChild;

            if (FindChild<T>(child, childName) is { } result)
                return result;
        }
        return null;
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
