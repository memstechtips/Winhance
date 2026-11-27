using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingOutcomeOverlay : SettingBoundControl
{
    public SettingOutcomeOverlay()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingOutcomeOverlay),
        new PropertyMetadata(SettingInputMode.Single));

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
        new PropertyMetadata(false));

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    public static readonly DependencyProperty IsTwoStateProperty = DependencyProperty.Register(
        nameof(IsTwoState), typeof(bool), typeof(SettingOutcomeOverlay),
        new PropertyMetadata(false));

    public bool IsTwoState
    {
        get => (bool)GetValue(IsTwoStateProperty);
        set => SetValue(IsTwoStateProperty, value);
    }

    // The trailing arguments are what can move the outcome: the outcome itself, the per-mode powercfg indices, and
    // SelectedValue, off which a detect-only state is read (its outcome resolves). x:Bind calls the function again
    // when any of them changes; the value itself comes from the view model.
    // x:Load cannot sit on a control's root, so the root stays and its parts are built only while it shows.
    public static bool Shows(SettingItemViewModel? setting, SettingInputMode mode,
        SettingDetectionOutcome _1, int _2, int _3, object? _4)
        => setting?.OverlayVisibilityForMode(mode) == Visibility.Visible;

    // The icons are a severity scale; a state simply not on the option list (a detect-only state, named by Text)
    // shows the name alone - a fault marker would assert a problem that is not there.
    public static bool ShowsIcon(SettingItemViewModel? setting, SettingInputMode mode,
        SettingDetectionOutcome _1, int _2, int _3, object? _4)
        => setting is not null && setting.OverlayVisibilityForMode(mode) == Visibility.Visible
            && setting.OverlayShowsIconForMode(mode);

    public static FluentIcons.Common.Icon Icon(SettingItemViewModel? setting, SettingInputMode mode,
        SettingDetectionOutcome _1, int _2, int _3, object? _4)
        => setting?.OverlayIconForMode(mode) ?? FluentIcons.Common.Icon.QuestionCircle;

    public static string Text(SettingItemViewModel? setting, SettingInputMode mode,
        SettingDetectionOutcome _1, int _2, int _3, object? _4)
        => setting?.OverlayTextForMode(mode) ?? string.Empty;

    public static string Tooltip(SettingItemViewModel? setting, SettingInputMode mode, bool isTwoState,
        SettingDetectionOutcome _1, int _2, int _3, object? _4)
        => setting?.OverlayTooltipForMode(mode, isTwoState) ?? string.Empty;

    public static double OpacityFor(bool isApplying) => isApplying ? 0d : 1d;

    // An Opacity-0 element is still clickable and still announced by Narrator, so while the marker is hidden
    // mid-apply both are switched off too.
    public static bool HitTestable(bool isInteractive, bool isApplying) => isInteractive && !isApplying;

    public static AccessibilityView AccessibilityViewFor(bool isApplying)
        => isApplying ? AccessibilityView.Raw : AccessibilityView.Content;
}
