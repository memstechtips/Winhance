using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Code-behind for <see cref="SettingOutcomeOverlay"/>. Exposes the two inputs every shared input control
/// passes down - the setting and which power mode this instance edits - and projects the view model's
/// outcome map into plain bindable properties.
///
/// The projected properties exist because x:Bind cannot call a method with a DependencyProperty argument
/// and re-evaluate when EITHER changes. Recomputing them whenever Setting, Mode, or the setting's own
/// state changes keeps the markup declarative and the refresh correct.
/// </summary>
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

    /// <summary>True for a toggle-style host, so the tooltip uses the toggle wording ("click the toggle")
    /// rather than the selection wording ("pick an option from the list").</summary>
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
    public FluentIcons.Common.Icon OverlayIcon { get; private set; } = FluentIcons.Common.Icon.QuestionCircle;
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

    /// <summary>Refresh on anything that can move the outcome: the outcome itself, the per-mode powercfg
    /// indices, and a language change (which re-raises the localized text properties).</summary>
    private void OnSettingPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(SettingItemViewModel.Outcome)
            or nameof(SettingItemViewModel.AcValue)
            or nameof(SettingItemViewModel.DcValue)
            or nameof(SettingItemViewModel.CustomStateText))
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
        OverlayIcon = vm.OverlayIconForMode(Mode);
        OverlayText = vm.OverlayTextForMode(Mode);
        OverlayTooltip = vm.OverlayTooltipForMode(Mode, IsToggleLike);
        Notify(nameof(OverlayVisibility), nameof(OverlayIcon), nameof(OverlayText), nameof(OverlayTooltip));
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
