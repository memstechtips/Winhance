using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
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

    private readonly ILocalizationService? _localizationService;
    private readonly List<WizardActionCard> _subscribedCards = new();
    private readonly List<WizardActionCard> _buildCards = new();
    private readonly Action _stopScrollRestore;

    public WimUtilViewModel ViewModel { get; }

    public WimUtilPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<WimUtilViewModel>();
        _localizationService = App.Services.GetService<ILocalizationService>();

        // PageUp/PageDown fast-scroll + Home/End jump (issue #581).
        PageScrollHelper.Attach(this, PageScrollView);
        _stopScrollRestore = ScrollMemory.Keep(nameof(WimUtilPage), PageScrollView);
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

            PageScrollView.ScrollTo(PageScrollView.HorizontalOffset, Math.Max(0, top.Value - StepScrollMargin),
                new ScrollingScrollOptions(ScrollingAnimationMode.Auto));
        });
    }

    private void OnWinhanceXmlInvoked(object sender, RoutedEventArgs e) => ViewModel.Step2.ChooseBuilderCommand.Execute(null);

    private void OnOwnXmlInvoked(object sender, RoutedEventArgs e) => ViewModel.Step2.ChooseOwnXmlCommand.Execute(null);

    private void OnPageScrollChanged(ScrollView sender, object args) => TrackCurrentStep();

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
        StepRail.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
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
            tops, PageScrollView.VerticalOffset, PageScrollView.ViewportHeight, PageScrollView.ScrollableHeight));
    }

    private WizardStep[] StepViews => [Step1View, Step2View, Step3View, Step4View];

    // Null until the step is in the tree: ScrollView's ViewChanged also fires during the first layout.
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
            ViewModel.SelectIsoCard, ViewModel.SelectDirectoryCard,
            ViewModel.SelectOutputCard, ViewModel.SelectUsbCard,
        };

        foreach (var card in cards)
        {
            if (card == null) continue;
            card.PropertyChanged += OnCardPropertyChanged;
            _subscribedCards.Add(card);
        }

        // The build cards are rebuilt on every Create, so they follow the collection.
        ViewModel.BuildCards.CollectionChanged += OnBuildCardsChanged;
        SubscribeToBuildCards();
    }

    private void OnBuildCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SubscribeToBuildCards();

        // A run clears the list and adds its first card, which is when the user needs to see the progress below the button.
        if (e.Action == NotifyCollectionChangedAction.Add && ViewModel.BuildCards.Count == 1)
            ScrollToBuildProgress();
    }

    private void ScrollToBuildProgress()
    {
        _stopScrollRestore();

        // The list only takes its place once layout has run with the new card in it.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (!BuildCardsList.IsLoaded) return;
            var top = BuildCardsList.TransformToVisual(StepsHost).TransformPoint(new Point(0, 0)).Y;
            PageScrollView.ScrollTo(PageScrollView.HorizontalOffset, Math.Max(0, top - StepScrollMargin),
                new ScrollingScrollOptions(ScrollingAnimationMode.Auto));
        });
    }

    private void SubscribeToBuildCards()
    {
        UnsubscribeFromBuildCards();
        foreach (var card in ViewModel.BuildCards)
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

        ViewModel.BuildCards.CollectionChanged -= OnBuildCardsChanged;
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
