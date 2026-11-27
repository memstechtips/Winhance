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
        nameof(Mode), typeof(SettingInputMode), typeof(SettingQuickSetButtons), new PropertyMetadata(SettingInputMode.Single));

    public SettingInputMode Mode
    {
        get => (SettingInputMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(SettingQuickSetKind), typeof(SettingQuickSetButtons), new PropertyMetadata(SettingQuickSetKind.Selection));

    public SettingQuickSetKind Kind
    {
        get => (SettingQuickSetKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    // The buttons show only while the info badges are on, which is the one thing that moves after the view model is
    // built; the commands are fixed per view model. The tooltips are re-read on a language switch (any of the view
    // model's localized properties raises then) and on a new selection, which follows a keyed option list rebuild.
    public static bool Shows(SettingItemViewModel? setting, SettingQuickSetKind kind, SettingInputMode mode, bool _)
        => setting is { } vm && kind switch
        {
            SettingQuickSetKind.TwoState => vm.ShowTwoStateQuickSetButtons,
            SettingQuickSetKind.Numeric => vm.ShowNumericQuickSetButtons,
            _ => vm.ShowSelectionQuickSetForMode(mode),
        };

    public static IRelayCommand? RecommendedCommand(SettingItemViewModel? setting, SettingQuickSetKind kind, SettingInputMode mode)
        => kind switch
        {
            SettingQuickSetKind.TwoState => setting?.SetTwoStateToRecommendedCommand,
            SettingQuickSetKind.Numeric => setting?.NumericRecommendedCommandForMode(mode),
            _ => setting?.SelectionRecommendedCommandForMode(mode),
        };

    public static IRelayCommand? DefaultCommand(SettingItemViewModel? setting, SettingQuickSetKind kind, SettingInputMode mode)
        => kind switch
        {
            SettingQuickSetKind.TwoState => setting?.SetTwoStateToDefaultCommand,
            SettingQuickSetKind.Numeric => setting?.NumericDefaultCommandForMode(mode),
            _ => setting?.SelectionDefaultCommandForMode(mode),
        };

    public static string? RecommendedTooltip(
        SettingItemViewModel? setting, SettingQuickSetKind kind, SettingInputMode mode, string _1, object? _2)
        => kind switch
        {
            SettingQuickSetKind.TwoState => setting?.TwoStateRecommendedTooltip,
            SettingQuickSetKind.Numeric => setting?.NumericRecommendedTooltipForMode(mode),
            _ => setting?.SelectionRecommendedTooltipForMode(mode),
        };

    public static string? DefaultTooltip(
        SettingItemViewModel? setting, SettingQuickSetKind kind, SettingInputMode mode, string _1, object? _2)
        => kind switch
        {
            SettingQuickSetKind.TwoState => setting?.TwoStateDefaultTooltip,
            SettingQuickSetKind.Numeric => setting?.NumericDefaultTooltipForMode(mode),
            _ => setting?.SelectionDefaultTooltipForMode(mode),
        };

    public static string RecommendedAutomationName(
        SettingItemViewModel? setting, SettingQuickSetKind kind, SettingInputMode mode, string _1, object? _2)
        => setting?.A11yNameForMode(mode, RecommendedTooltip(setting, kind, mode, _1, _2)) ?? string.Empty;

    public static string DefaultAutomationName(
        SettingItemViewModel? setting, SettingQuickSetKind kind, SettingInputMode mode, string _1, object? _2)
        => setting?.A11yNameForMode(mode, DefaultTooltip(setting, kind, mode, _1, _2)) ?? string.Empty;
}
