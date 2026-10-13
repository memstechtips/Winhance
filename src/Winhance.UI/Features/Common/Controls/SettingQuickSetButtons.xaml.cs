using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

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

    public Visibility ButtonsVisibility { get; private set; } = Visibility.Collapsed;
    public IRelayCommand? RecommendedCommand { get; private set; }
    public IRelayCommand? DefaultCommand { get; private set; }
    public string? RecommendedTooltip { get; private set; }
    public string? DefaultTooltip { get; private set; }
    public string RecommendedAutomationName { get; private set; } = string.Empty;
    public string DefaultAutomationName { get; private set; } = string.Empty;

    protected override void Refresh()
    {
        if (Setting is not { } vm)
        {
            ButtonsVisibility = Visibility.Collapsed;
            NotifyAll();
            return;
        }

        bool show;
        switch (Kind)
        {
            case SettingQuickSetKind.TwoState:
                show = vm.ShowTwoStateQuickSetButtons;
                RecommendedCommand = vm.SetTwoStateToRecommendedCommand;
                DefaultCommand = vm.SetTwoStateToDefaultCommand;
                RecommendedTooltip = vm.TwoStateRecommendedTooltip;
                DefaultTooltip = vm.TwoStateDefaultTooltip;
                break;

            case SettingQuickSetKind.Numeric:
                show = vm.ShowNumericQuickSetButtons;
                RecommendedCommand = vm.NumericRecommendedCommandForMode(Mode);
                DefaultCommand = vm.NumericDefaultCommandForMode(Mode);
                RecommendedTooltip = vm.NumericRecommendedTooltipForMode(Mode);
                DefaultTooltip = vm.NumericDefaultTooltipForMode(Mode);
                break;

            default:
                show = vm.ShowSelectionQuickSetForMode(Mode);
                RecommendedCommand = vm.SelectionRecommendedCommandForMode(Mode);
                DefaultCommand = vm.SelectionDefaultCommandForMode(Mode);
                RecommendedTooltip = vm.SelectionRecommendedTooltipForMode(Mode);
                DefaultTooltip = vm.SelectionDefaultTooltipForMode(Mode);
                break;
        }

        ButtonsVisibility = show ? Visibility.Visible : Visibility.Collapsed;
        RecommendedAutomationName = vm.A11yNameForMode(Mode, RecommendedTooltip);
        DefaultAutomationName = vm.A11yNameForMode(Mode, DefaultTooltip);
        NotifyAll();
    }

    private void NotifyAll() => Notify(
        nameof(ButtonsVisibility), nameof(RecommendedCommand), nameof(DefaultCommand),
        nameof(RecommendedTooltip), nameof(DefaultTooltip),
        nameof(RecommendedAutomationName), nameof(DefaultAutomationName));
}
