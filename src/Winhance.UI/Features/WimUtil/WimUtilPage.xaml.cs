using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Winhance.UI.Features.WimUtil.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.WimUtil.ViewModels;
using Winhance.UI.Features.Common.Helpers;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.WimUtil;

public sealed partial class WimUtilPage : Page
{
    // Keeps the step header clear of the top edge after a rail jump.
    private const double StepScrollMargin = 8;

    // WizardStepRail's width plus the gap to the steps.
    private const double RailColumnWidth = 248;

    // Below this page width the rail gives its room back to the steps: SettingsContentMargin on both sides, the rail
    // column and 720 for the steps, under which the Step 1 card rows wrap word by word.
    private const double RailMinPageWidth = 2 * 24 + RailColumnWidth + 720;

    private readonly ILocalizationService _localizationService;
    private readonly List<WizardActionCard> _subscribedCards = new();
    private readonly List<WizardActionCard> _buildCards = new();
    private readonly Action _stopScrollRestore;

    // Notches that land while the last one is still animating add to its target instead of restarting from mid-way.
    private double? _railWheelTarget;

    public WimUtilViewModel ViewModel { get; }

    public WimUtilPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<WimUtilViewModel>();
        _localizationService = App.Services.GetRequiredService<ILocalizationService>();

        // PageUp/PageDown fast-scroll + Home/End jump (issue #581).
        PageScrollHelper.Attach(this, PageScrollViewer);
        _stopScrollRestore = ScrollMemory.Keep(nameof(WimUtilPage), PageScrollViewer);
    }

    private void OnRailWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(StepRail);
        if (point.Properties.IsHorizontalMouseWheel) return;

        var from = _railWheelTarget ?? PageScrollViewer.VerticalOffset;
        _railWheelTarget = Math.Clamp(from - point.Properties.MouseWheelDelta, 0, PageScrollViewer.ScrollableHeight);
        PageScrollViewer.ChangeView(null, _railWheelTarget, null);
        e.Handled = true;
    }

    private void OnRailStepInvoked(object? sender, int step)
    {
        if (!ViewModel.ShowStep(step)) return;
        _stopScrollRestore();

        // Expanding the step moves the ones below it, so its top is read once layout has run.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            var top = StepTop(step);
            if (top is null) return;

            PageScrollViewer.ChangeView(null, Math.Max(0, top.Value - StepScrollMargin), null);
        });
    }

    private void OnWinhanceXmlInvoked(object sender, RoutedEventArgs e) => ViewModel.Step2.ChooseBuilderCommand.Execute(null);

    private void OnOwnXmlInvoked(object sender, RoutedEventArgs e) => ViewModel.Step2.ChooseOwnXmlCommand.Execute(null);

    private void OnPageScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (!e.IsIntermediate)
            _railWheelTarget = null;
        TrackCurrentStep();
    }

    private void OnStepsSizeChanged(object sender, SizeChangedEventArgs e) => TrackCurrentStep();

    private void OnPageScrollSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ShowRail(e.NewSize.Width >= RailMinPageWidth);
        TrackCurrentStep();
    }

    // The rail and the steps share one column layout, so this only opens or closes the rail's column in both grids.
    private void ShowRail(bool show)
    {
        var width = new GridLength(show ? RailColumnWidth : 0);
        StepsRailColumn.Width = width;
        RailColumn.Width = width;
        if (!show)
        {
            if (StepRail is { } built)
                UnloadObject(built);
            return;
        }

        if (StepRail is not null)
            return;

        // The rail is drawn over the scroller, not inside it, so a wheel over it would otherwise move nothing.
        var rail = (WizardStepRail)FindName(nameof(StepRail));
        rail.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnRailWheelChanged), handledEventsToo: true);
    }

    private void TrackCurrentStep()
    {
        var tops = new List<double>(StepViews.Length);
        for (var step = 1; step <= StepViews.Length; step++)
        {
            if (StepTop(step) is not double top) return;
            tops.Add(top);
        }

        ViewModel.SetCurrentStep(WizardStepTracker.CurrentStep(
            tops, PageScrollViewer.VerticalOffset, PageScrollViewer.ViewportHeight, PageScrollViewer.ScrollableHeight));
    }

    private WizardStep[] StepViews => [Step1View, Step2View, Step3View, Step4View];

    // Null until the step is in the tree: the scroller's view and size events also fire during the first layout.
    private double? StepTop(int step)
    {
        var view = StepViews[step - 1];
        return view.IsLoaded
            ? view.TransformToVisual(StepsHost).TransformPoint(new Point(0, 0)).Y
            : null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel.OnNavigatedTo();

        // Live-region announcements when wizard action cards transition state (issue #647).
        SubscribeToActionCards();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        UnsubscribeFromActionCards();
    }

    private void SubscribeToActionCards()
    {
        UnsubscribeFromActionCards();

        // Every card the user can see a spinner/checkmark/X on.
        WizardActionCard?[] cards =
        {
            ViewModel.Step1.SelectIsoCard, ViewModel.Step1.SelectDirectoryCard,
            ViewModel.Step5.SelectOutputCard, ViewModel.Step5.SelectUsbCard,
        };

        foreach (var card in cards)
        {
            if (card == null) continue;
            card.PropertyChanged += OnCardPropertyChanged;
            _subscribedCards.Add(card);
        }

        // The build cards are rebuilt on every Create, so they follow the collection.
        ViewModel.Step5.BuildCards.CollectionChanged += OnBuildCardsChanged;
        SubscribeToBuildCards();
    }

    private void OnBuildCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SubscribeToBuildCards();

        // A run clears the list and adds its first card, which is when the user needs to see the progress below the button.
        if (e.Action == NotifyCollectionChangedAction.Add && ViewModel.Step5.BuildCards.Count == 1)
            ScrollToBuildProgress();
    }

    private void ScrollToBuildProgress()
    {
        _stopScrollRestore();

        // The list only takes its place once layout has run with the new card in it.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (BuildCardsList is not { IsLoaded: true }) return;
            var top = BuildCardsList.TransformToVisual(StepsHost).TransformPoint(new Point(0, 0)).Y;
            PageScrollViewer.ChangeView(null, Math.Max(0, top - StepScrollMargin), null);
        });
    }

    private void SubscribeToBuildCards()
    {
        UnsubscribeFromBuildCards();
        foreach (var card in ViewModel.Step5.BuildCards)
        {
            card.PropertyChanged += OnCardPropertyChanged;
            _buildCards.Add(card);
        }
    }

    private void UnsubscribeFromBuildCards()
    {
        foreach (var card in _buildCards)
            card.PropertyChanged -= OnCardPropertyChanged;
        _buildCards.Clear();
    }

    private void UnsubscribeFromActionCards()
    {
        foreach (var card in _subscribedCards)
        {
            card.PropertyChanged -= OnCardPropertyChanged;
        }
        _subscribedCards.Clear();

        ViewModel.Step5.BuildCards.CollectionChanged -= OnBuildCardsChanged;
        UnsubscribeFromBuildCards();
    }

    private void OnCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WizardActionCard card) return;

        // Only announce state transitions into a "noteworthy" state — the source-generated
        // PropertyChanged only fires on actual value changes, so this won't repeat.
        string? message = e.PropertyName switch
        {
            nameof(WizardActionCard.IsProcessing) when card.IsProcessing
                => $"{card.Title}: {_localizationService.GetStringOrDefault("Accessibility_InProgress", "in progress")}",
            nameof(WizardActionCard.IsComplete) when card.IsComplete
                => $"{card.Title}: {_localizationService.GetStringOrDefault("Accessibility_Complete", "complete")}",
            nameof(WizardActionCard.HasFailed) when card.HasFailed
                => $"{card.Title}: {_localizationService.GetStringOrDefault("Accessibility_Failed", "failed")}",
            _ => null,
        };
        if (message != null) Announce(message);
    }

    private void Announce(string message)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(this)
                   ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);

        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent,
            message,
            "WimUtil");
    }
}
