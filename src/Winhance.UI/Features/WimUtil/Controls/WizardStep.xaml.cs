using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.Controls;

// One WIMUtil step: a header card that runs Command to expand or collapse, with Body below it.
public sealed partial class WizardStep : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(WizardStepState),
        typeof(WizardStep),
        new PropertyMetadata(null));

    public WizardStepState? State
    {
        get => (WizardStepState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(WizardStep),
        new PropertyMetadata(null));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(WizardStep),
        new PropertyMetadata(null));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body),
        typeof(object),
        typeof(WizardStep),
        new PropertyMetadata(null));

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public WizardStep()
    {
        InitializeComponent();
    }

    // Fluent's disabled look for content that is not text, so a locked step's icon dims with its title.
    public static double AvailableOpacity(bool isAvailable) => isAvailable ? 1 : 0.4;

    // A tooltip that only shows while its control is locked, saying why.
    public static string? LockedReason(bool isAvailable, string reason) => isAvailable ? null : reason;

    public static CornerRadius HeaderCornerRadius(bool isExpanded) =>
        isExpanded ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(4);

    // A locked step shows only the padlock; a finished one swaps the chevron for the check.
    public static Visibility CompleteVisibility(bool isComplete, bool isLocked) =>
        isComplete && !isLocked ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ChevronVisibility(bool isComplete, bool isLocked) =>
        !isComplete && !isLocked ? Visibility.Visible : Visibility.Collapsed;

    private void OnHeaderInvoked(object sender, RoutedEventArgs e) => Command?.Execute(CommandParameter);
}
