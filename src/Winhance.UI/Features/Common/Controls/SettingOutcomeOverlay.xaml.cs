using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// The projected properties exist because x:Bind cannot call a method with a DependencyProperty argument and
// re-evaluate when EITHER changes.
public sealed partial class SettingOutcomeOverlay : SettingBoundControl
{
    public SettingOutcomeOverlay()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingOutcomeOverlay),
        new PropertyMetadata(SettingInputMode.Single, RefreshOnChange));

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
        new PropertyMetadata(false, RefreshOnChange));

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    public static readonly DependencyProperty IsTwoStateProperty = DependencyProperty.Register(
        nameof(IsTwoState), typeof(bool), typeof(SettingOutcomeOverlay),
        new PropertyMetadata(false, RefreshOnChange));

    public bool IsTwoState
    {
        get => (bool)GetValue(IsTwoStateProperty);
        set => SetValue(IsTwoStateProperty, value);
    }

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

    // Anything that can move the outcome: the outcome itself, the per-mode powercfg indices, a language change
    // (which re-raises the localized text), and IsApplying, which hides the marker during an apply.
    protected override bool RefreshesOn(string propertyName)
        => propertyName is nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.AcValue)
            or nameof(SettingItemViewModel.DcValue)
            // A detect-only current state is read off SelectedValue, not off Outcome (it resolves), so
            // without this the overlay would keep drawing the previous state's name after a change.
            or nameof(SettingItemViewModel.SelectedValue)
            or nameof(SettingItemViewModel.CustomStateText)
            or nameof(SettingItemViewModel.IsApplying);

    protected override void Refresh()
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
        OverlayTooltip = vm.OverlayTooltipForMode(Mode, IsTwoState);
        Notify(nameof(OverlayVisibility), nameof(OverlayOpacity), nameof(OverlayHitTestable),
            nameof(OverlayAccessibilityView), nameof(OverlayIcon), nameof(OverlayIconVisibility),
            nameof(OverlayText), nameof(OverlayTooltip));
    }
}
