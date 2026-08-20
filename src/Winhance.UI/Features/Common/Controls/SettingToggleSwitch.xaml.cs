using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// Owns the overlay knob's hover animation: the icon is named here, which x:Name inside a DataTemplate would not allow.
public sealed partial class SettingToggleSwitch : UserControl, INotifyPropertyChanged
{
    // Matches the native ToggleSwitch's knob-grow on pointer-over.
    private const double HoverScale = 1.15;
    private const double RestScale = 1.0;

    public SettingToggleSwitch()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingToggleSwitch),
        new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public bool IsOn { get; private set; }
    public string OnText { get; private set; } = string.Empty;
    public string OffText { get; private set; } = string.Empty;
    public string SettingName { get; private set; } = string.Empty;

    public double SwitchOpacity { get; private set; } = 1d;
    public bool SwitchInteractive { get; private set; } = true;
    public AccessibilityView SwitchAccessibilityView { get; private set; } = AccessibilityView.Content;

    public Visibility OverlayVisibility { get; private set; } = Visibility.Collapsed;
    public FluentIcons.Common.Icon OverlayIcon { get; private set; } = FluentIcons.Common.Icon.QuestionCircle;
    public string OverlayShortLabel { get; private set; } = string.Empty;
    public string OverlayText { get; private set; } = string.Empty;
    public string OverlayTooltip { get; private set; } = string.Empty;

    private SettingItemViewModel? _observed;

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingToggleSwitch)d;
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

    // A null PropertyName means "everything", which the view model raises on a language change; the
    // named ones are what actually move a toggle's rendering.
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

        IsOn = vm.IsSelected;
        OnText = vm.OnText;
        OffText = vm.OffText;
        SettingName = vm.Name;

        SwitchOpacity = vm.ToggleOpacityFor(vm.Outcome);
        SwitchInteractive = vm.ToggleInteractiveFor(vm.Outcome);
        SwitchAccessibilityView = vm.ToggleAccessibilityViewFor(vm.Outcome);

        OverlayVisibility = vm.OverlayVisibilityFor(vm.Outcome);
        OverlayIcon = vm.OverlayIconFor(vm.Outcome);
        OverlayShortLabel = vm.OverlayShortLabelFor(vm.Outcome);
        OverlayText = vm.OverlayStateTextFor(vm.Outcome);
        OverlayTooltip = vm.OverlayTooltipFor(vm.Outcome);

        Notify(
            nameof(IsOn), nameof(OnText), nameof(OffText), nameof(SettingName),
            nameof(SwitchOpacity), nameof(SwitchInteractive), nameof(SwitchAccessibilityView),
            nameof(OverlayVisibility), nameof(OverlayIcon), nameof(OverlayShortLabel),
            nameof(OverlayText), nameof(OverlayTooltip));
    }

    private void OnToggled(object sender, RoutedEventArgs e) => Setting?.OnToggleSwitchToggled(sender);

    private void OnOverlayClicked(object sender, RoutedEventArgs e) => Setting?.OnCustomToggleClicked();

    private void OnOverlayPointerEntered(object sender, PointerRoutedEventArgs e) => AnimateKnob(HoverScale);

    // Wired to PointerExited AND PointerCanceled AND PointerCaptureLost so the knob can never stick enlarged.
    private void OnOverlayPointerExited(object sender, PointerRoutedEventArgs e) => AnimateKnob(RestScale);

    private void AnimateKnob(double scale)
    {
        if (KnobIcon.RenderTransform is not CompositeTransform transform)
            return;

        var duration = new Duration(TimeSpan.FromMilliseconds(100));
        var storyboard = new Storyboard();
        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            // EnableDependentAnimation: a storyboard targeting the CompositeTransform object directly is
            // classified as a dependent animation; without the flag it silently does nothing.
            var animation = new DoubleAnimation { To = scale, Duration = duration, EnableDependentAnimation = true };
            Storyboard.SetTarget(animation, transform);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }
        storyboard.Begin();
    }

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
