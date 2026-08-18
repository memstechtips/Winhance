using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// The projected properties exist because x:Bind cannot call a method with a DependencyProperty argument and
// re-evaluate when EITHER changes.
public sealed partial class SettingOutcomeOverlay : UserControl, INotifyPropertyChanged
{
    public SettingOutcomeOverlay()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingOutcomeOverlay),
        new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingOutcomeOverlay),
        new PropertyMetadata(SettingInputMode.Single, OnAnyChanged));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    // False (default) lets clicks reach the control underneath - which also means a tooltip on this element can
    // never fire, so those hosts put it on the control instead. True when the control underneath is inert while
    // unresolved, so intercepting costs nothing and the tooltip works.
    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(
        nameof(IsInteractive), typeof(bool), typeof(SettingOutcomeOverlay),
        new PropertyMetadata(false, OnAnyChanged));

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    // Toggle wording ("click the toggle") vs selection wording ("pick an option from the list").
    public static readonly DependencyProperty IsToggleLikeProperty = DependencyProperty.Register(
        nameof(IsToggleLike), typeof(bool), typeof(SettingOutcomeOverlay),
        new PropertyMetadata(false, OnAnyChanged));

    public bool IsToggleLike
    {
        get => (bool)GetValue(IsToggleLikeProperty);
        set => SetValue(IsToggleLikeProperty, value);
    }

    // --- Projected, bindable ---------------------------------------------------------------------

    public Visibility OverlayVisibility { get; private set; } = Visibility.Collapsed;
    public double OverlayOpacity { get; private set; } = 1d;

    // An Opacity-0 element is still clickable and still announced by Narrator, so while the marker is hidden
    // mid-apply both are switched off too.
    public bool OverlayHitTestable { get; private set; }
    public AccessibilityView OverlayAccessibilityView { get; private set; } = AccessibilityView.Content;
    public FluentIcons.Common.Icon OverlayIcon { get; private set; } = FluentIcons.Common.Icon.QuestionCircle;

    // The icons are a severity scale; a state simply not on the option list (a detect-only state, named by
    // OverlayText) shows the name alone - a fault marker would assert a problem that is not there.
    public Visibility OverlayIconVisibility { get; private set; } = Visibility.Visible;
    public string OverlayText { get; private set; } = string.Empty;
    public string OverlayTooltip { get; private set; } = string.Empty;

    private SettingItemViewModel? _observed;

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var overlay = (SettingOutcomeOverlay)d;
        overlay.Detach();
        overlay._observed = e.NewValue as SettingItemViewModel;
        if (overlay._observed is { } vm)
            vm.PropertyChanged += overlay.OnSettingPropertyChanged;
        overlay.Refresh();
    }

    private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SettingOutcomeOverlay)d).Refresh();

    private void Detach()
    {
        if (_observed is { } vm)
            vm.PropertyChanged -= OnSettingPropertyChanged;
        _observed = null;
    }

    // Anything that can move the outcome: the outcome itself, the per-mode powercfg indices, a language change
    // (which re-raises the localized text), and IsApplying, which hides the marker during an apply.
    private void OnSettingPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.AcValue)
            or nameof(SettingItemViewModel.DcValue)
            // A detect-only current state is read off SelectedValue, not off Outcome (it resolves), so
            // without this the overlay would keep drawing the previous state's name after a change.
            or nameof(SettingItemViewModel.SelectedValue)
            or nameof(SettingItemViewModel.CustomStateText)
            or nameof(SettingItemViewModel.IsApplying))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (Setting is not { } vm)
        {
            OverlayVisibility = Visibility.Collapsed;
            Notify(nameof(OverlayVisibility));
            return;
        }

        OverlayVisibility = vm.OverlayVisibilityForMode(Mode);
        OverlayOpacity = vm.OverlayOpacity;
        OverlayHitTestable = IsInteractive && !vm.IsApplying;
        OverlayAccessibilityView = vm.IsApplying ? AccessibilityView.Raw : AccessibilityView.Content;
        OverlayIcon = vm.OverlayIconForMode(Mode);
        OverlayIconVisibility = vm.OverlayShowsIconForMode(Mode) ? Visibility.Visible : Visibility.Collapsed;
        OverlayText = vm.OverlayTextForMode(Mode);
        OverlayTooltip = vm.OverlayTooltipForMode(Mode, IsToggleLike);
        Notify(nameof(OverlayVisibility), nameof(OverlayOpacity), nameof(OverlayHitTestable),
            nameof(OverlayAccessibilityView), nameof(OverlayIcon), nameof(OverlayIconVisibility),
            nameof(OverlayText), nameof(OverlayTooltip));
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
}
