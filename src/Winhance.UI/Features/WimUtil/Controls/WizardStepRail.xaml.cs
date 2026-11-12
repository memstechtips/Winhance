using System.Collections;
using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Windows.UI.Text;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.Controls;

// The step list beside the wizard: a marker per step (its icon, on green once done) and a connector between
// steps. It is a ListView so arrow keys, hover, focus visuals and screen-reader list semantics come from the
// platform; the step in view is its selected item, which draws the NavigationView-style pill. Clicking a step
// raises StepInvoked.
public sealed partial class WizardStepRail : UserControl
{
    public static readonly DependencyProperty StepsProperty = DependencyProperty.Register(
        nameof(Steps),
        typeof(IEnumerable),
        typeof(WizardStepRail),
        new PropertyMetadata(null, OnStepsChanged));

    public IEnumerable? Steps
    {
        get => (IEnumerable?)GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public event EventHandler<int>? StepInvoked;

    public WizardStepRail()
    {
        InitializeComponent();
    }

    public static Visibility PendingVisibility(bool isComplete) =>
        isComplete ? Visibility.Collapsed : Visibility.Visible;

    public static FontWeight LabelWeight(bool isCurrent) => isCurrent ? FontWeights.SemiBold : FontWeights.Normal;

    private static void OnStepsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var rail = (WizardStepRail)d;
        foreach (var step in StepsOf(e.OldValue))
            step.PropertyChanged -= rail.OnStepPropertyChanged;
        foreach (var step in StepsOf(e.NewValue))
            step.PropertyChanged += rail.OnStepPropertyChanged;
        rail.SelectCurrentStep();
    }

    private static IEnumerable<WizardStepState> StepsOf(object? steps) =>
        (steps as IEnumerable)?.OfType<WizardStepState>() ?? [];

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WizardStepState.IsCurrent))
            SelectCurrentStep();
    }

    // Selection only mirrors the step in view: a Space press or a click on a step selects it before the page has
    // scrolled, so anything else is put back.
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => SelectCurrentStep();

    private void SelectCurrentStep()
    {
        var current = StepsOf(Steps).FirstOrDefault(s => s.IsCurrent);
        if (!ReferenceEquals(RailList.SelectedItem, current))
            RailList.SelectedItem = current;
    }

    // WinUI applies no Binding inside a Style Setter, so the container's name, help text and enabled state are bound
    // here, to the step itself: the container's DataContext is not the item.
    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.ItemContainer is not { } container) return;

        container.SetBinding(AutomationProperties.NameProperty, OneWay(args.Item, nameof(WizardStepState.AccessibleName)));
        container.SetBinding(AutomationProperties.HelpTextProperty, OneWay(args.Item, nameof(WizardStepState.StatusText)));
        container.SetBinding(IsEnabledProperty, OneWay(args.Item, nameof(WizardStepState.IsAvailable)));
    }

    private static Binding OneWay(object source, string path) =>
        new() { Source = source, Path = new PropertyPath(path), Mode = BindingMode.OneWay };

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WizardStepState step)
            StepInvoked?.Invoke(this, step.StepNumber);
    }
}
