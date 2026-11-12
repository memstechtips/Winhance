using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// The commands differ per control type as well as per power mode; both axes are resolved on the view model.
public enum SettingQuickSetKind
{
    TwoState,
    Selection,
    Numeric,
}

public sealed partial class SettingQuickSetButtons : SettingBoundControl
{
    public SettingQuickSetButtons()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(SettingInputMode), typeof(SettingQuickSetButtons),
        new PropertyMetadata(SettingInputMode.Single, RefreshOnChange));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(SettingQuickSetKind), typeof(SettingQuickSetButtons),
        new PropertyMetadata(SettingQuickSetKind.Selection, RefreshOnChange));

    public SettingQuickSetKind Kind
    {
        get => (SettingQuickSetKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private QuickSetState _state = QuickSetState.Hidden;

    public Visibility ButtonsVisibility => _state.Show ? Visibility.Visible : Visibility.Collapsed;
    public IRelayCommand? RecommendedCommand => _state.RecommendedCommand;
    public IRelayCommand? DefaultCommand => _state.DefaultCommand;
    public string? RecommendedTooltip => _state.RecommendedTooltip;
    public string? DefaultTooltip => _state.DefaultTooltip;
    public string RecommendedAutomationName => _state.RecommendedAutomationName;
    public string DefaultAutomationName => _state.DefaultAutomationName;

    // Every view model change lands here (the inputs are spread over too many properties to filter safely), so only
    // the values that actually moved are announced: seven notifications per change per card added up on a long list.
    protected override void Refresh()
    {
        var next = Setting is { } vm ? Read(vm) : QuickSetState.Hidden;
        var changed = QuickSetState.Changed(_state, next);
        _state = next;
        if (changed.Length > 0)
            Notify(changed);
    }

    private QuickSetState Read(SettingItemViewModel vm)
    {
        var (show, recommended, @default, recommendedTip, defaultTip) = Kind switch
        {
            SettingQuickSetKind.TwoState => (vm.ShowTwoStateQuickSetButtons,
                vm.SetTwoStateToRecommendedCommand, vm.SetTwoStateToDefaultCommand,
                vm.TwoStateRecommendedTooltip, vm.TwoStateDefaultTooltip),
            SettingQuickSetKind.Numeric => (vm.ShowNumericQuickSetButtons,
                vm.NumericRecommendedCommandForMode(Mode), vm.NumericDefaultCommandForMode(Mode),
                vm.NumericRecommendedTooltipForMode(Mode), vm.NumericDefaultTooltipForMode(Mode)),
            _ => (vm.ShowSelectionQuickSetForMode(Mode),
                vm.SelectionRecommendedCommandForMode(Mode), vm.SelectionDefaultCommandForMode(Mode),
                vm.SelectionRecommendedTooltipForMode(Mode), vm.SelectionDefaultTooltipForMode(Mode)),
        };

        return new QuickSetState(show, recommended, @default, recommendedTip, defaultTip,
            vm.A11yNameForMode(Mode, recommendedTip), vm.A11yNameForMode(Mode, defaultTip));
    }
}

internal readonly record struct QuickSetState(
    bool Show,
    IRelayCommand? RecommendedCommand,
    IRelayCommand? DefaultCommand,
    string? RecommendedTooltip,
    string? DefaultTooltip,
    string RecommendedAutomationName,
    string DefaultAutomationName)
{
    public static QuickSetState Hidden { get; } = new(false, null, null, null, null, string.Empty, string.Empty);

    public static string[] Changed(QuickSetState before, QuickSetState after)
    {
        var names = new List<string>(7);
        if (before.Show != after.Show) names.Add(nameof(SettingQuickSetButtons.ButtonsVisibility));
        if (!ReferenceEquals(before.RecommendedCommand, after.RecommendedCommand)) names.Add(nameof(SettingQuickSetButtons.RecommendedCommand));
        if (!ReferenceEquals(before.DefaultCommand, after.DefaultCommand)) names.Add(nameof(SettingQuickSetButtons.DefaultCommand));
        if (before.RecommendedTooltip != after.RecommendedTooltip) names.Add(nameof(SettingQuickSetButtons.RecommendedTooltip));
        if (before.DefaultTooltip != after.DefaultTooltip) names.Add(nameof(SettingQuickSetButtons.DefaultTooltip));
        if (before.RecommendedAutomationName != after.RecommendedAutomationName) names.Add(nameof(SettingQuickSetButtons.RecommendedAutomationName));
        if (before.DefaultAutomationName != after.DefaultAutomationName) names.Add(nameof(SettingQuickSetButtons.DefaultAutomationName));
        return [.. names];
    }
}
