using System.Windows.Input;
using CommunityToolkit.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class SettingsListView : UserControl
{
    public static readonly DependencyProperty GroupedSettingsSourceProperty =
        DependencyProperty.Register(
            nameof(GroupedSettingsSource),
            typeof(ICollectionView),
            typeof(SettingsListView),
            new PropertyMetadata(null, OnGroupedSettingsSourceChanged));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty HasNoSearchResultsProperty =
        DependencyProperty.Register(
            nameof(HasNoSearchResults),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty NoResultsTextProperty =
        DependencyProperty.Register(
            nameof(NoResultsText),
            typeof(string),
            typeof(SettingsListView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsShowAllVisibleProperty =
        DependencyProperty.Register(
            nameof(IsShowAllVisible),
            typeof(bool),
            typeof(SettingsListView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowAllTextProperty =
        DependencyProperty.Register(
            nameof(ShowAllText),
            typeof(string),
            typeof(SettingsListView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowAllCommandProperty =
        DependencyProperty.Register(
            nameof(ShowAllCommand),
            typeof(ICommand),
            typeof(SettingsListView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(SettingsListView),
            new PropertyMetadata(null));

    public ICollectionView? GroupedSettingsSource
    {
        get => (ICollectionView?)GetValue(GroupedSettingsSourceProperty);
        set => SetValue(GroupedSettingsSourceProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    // The ListView builds its item containers over several frames after it turns visible, and on a long page
    // those frames are blank. The loader stays up over the hidden list until the first item has its content.
    private bool _revealing;

    private ICollectionView? _pendingSource;
    private int _framesBeforeSource;

    public bool HasNoSearchResults
    {
        get => (bool)GetValue(HasNoSearchResultsProperty);
        set => SetValue(HasNoSearchResultsProperty, value);
    }

    public string NoResultsText
    {
        get => (string)GetValue(NoResultsTextProperty);
        set => SetValue(NoResultsTextProperty, value);
    }

    public bool IsShowAllVisible
    {
        get => (bool)GetValue(IsShowAllVisibleProperty);
        set => SetValue(IsShowAllVisibleProperty, value);
    }

    public string ShowAllText
    {
        get => (string)GetValue(ShowAllTextProperty);
        set => SetValue(ShowAllTextProperty, value);
    }

    public ICommand? ShowAllCommand
    {
        get => (ICommand?)GetValue(ShowAllCommandProperty);
        set => SetValue(ShowAllCommandProperty, value);
    }

    // Inside this control's scroller: a page that wraps its own ScrollView around the list gets two fighting over the wheel.
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public SettingsListView()
    {
        this.InitializeComponent();
        SettingsListViewControl.LosingFocus += ListView_LosingFocus;
        NoResultsText = App.Services.GetRequiredService<ILocalizationService>().GetString("Common_Search_NoResults");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // Set by a host that scrolls the list with the rest of its page (the WIMUtil wizard): a second scroller would
    // fight it for the wheel, so the list then gets unbounded height and realizes every card.
    public bool ScrollsWithPage { get; set; }

    // Set by a host on an uncached page, so a return visit opens where the user left the list.
    public string? ScrollMemoryKey { get; set; }

    private bool _scrollerSetUp;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_scrollerSetUp)
            SetUpScroller();

        if (SettingsListViewControl.ItemsSource is null && _framesBeforeSource == 0 && GroupedSettingsSource is { } source)
            DeferSource(source);
    }

    // A list collapsed behind the loader may have no template yet, so this also runs when the list turns visible.
    private void SetUpScroller()
    {
        SettingsListViewControl.ApplyTemplate();
        if (SettingsListViewControl.FindDescendant<ScrollViewer>() is not { } scroller)
            return;
        _scrollerSetUp = true;

        if (ScrollsWithPage)
        {
            ScrollViewer.SetVerticalScrollMode(SettingsListViewControl, ScrollMode.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(SettingsListViewControl, ScrollBarVisibility.Disabled);
        }
        else
        {
            // Listening on the UserControl root lets us see the key even after the
            // ListView marks it handled during focus traversal (issue #581).
            PageScrollHelper.Attach(this, scroller);
            if (ScrollMemoryKey is { } key)
                ScrollMemory.Track(key, SettingsListViewControl, scroller);
        }
    }

    // The cards' x:Bind trackers stay subscribed to the long-lived setting view models until their containers are
    // cleared, and a page dropped from its frame never clears them, so each visit would leave one more list listening.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_framesBeforeSource > 0)
        {
            CompositionTarget.Rendering -= OnRenderingBeforeSource;
            _framesBeforeSource = 0;
        }
        SettingsListViewControl.LayoutUpdated -= ListView_LayoutUpdated;
        SettingsListViewControl.ItemsSource = null;
        _revealing = false;
        ShowLoadingState();
    }

    // The ListView's TabFocusNavigation=Once forces Tab to exit after one stop; this redirects it to the
    // next/previous setting control inside the list.
    private void ListView_LosingFocus(object sender, LosingFocusEventArgs e)
    {
        if (e.InputDevice != FocusInputDeviceKind.Keyboard) return;

        // Direction=None with New=null means the ListView couldn't find a next item - treat as forward.
        bool isForward;
        if (e.Direction == FocusNavigationDirection.Next)
            isForward = true;
        else if (e.Direction == FocusNavigationDirection.Previous)
            isForward = false;
        else if (e.Direction == FocusNavigationDirection.None && e.NewFocusedElement == null)
            isForward = true;
        else
            return; // Arrow key navigation or other — don't intercept

        var oldElement = e.OldFocusedElement as DependencyObject;
        if (oldElement == null) return;

        DependencyObject? current = oldElement;
        while (current != null && current is not ListViewItem)
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);

        if (current is not ListViewItem currentItem) return;

        if (e.NewFocusedElement is DependencyObject newElement)
        {
            DependencyObject? newParent = newElement;
            while (newParent != null && newParent is not ListViewItem)
                newParent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(newParent);
            if (newParent == currentItem) return;
        }

        var currentIndex = SettingsListViewControl.IndexFromContainer(currentItem);
        if (currentIndex < 0) return;

        var itemCount = SettingsListViewControl.Items.Count;
        var step = isForward ? 1 : -1;

        for (var i = currentIndex + step; i >= 0 && i < itemCount; i += step)
        {
            var nextContainer = SettingsListViewControl.ContainerFromIndex(i) as ListViewItem;

            // If container isn't realized yet, scroll it into view to force realization
            if (nextContainer == null)
            {
                SettingsListViewControl.ScrollIntoView(SettingsListViewControl.Items[i]);
                SettingsListViewControl.UpdateLayout();
                nextContainer = SettingsListViewControl.ContainerFromIndex(i) as ListViewItem;
            }

            if (nextContainer == null) continue;

            var nextFocusable = isForward
                ? FocusManager.FindFirstFocusableElement(nextContainer)
                : FocusManager.FindLastFocusableElement(nextContainer);

            if (nextFocusable is DependencyObject nextTarget)
            {
                if (e.TrySetNewFocusedElement(nextTarget)) return;
            }
        }

        // At the boundary (first or last item) — let focus leave the list naturally
    }

    private void TechnicalDetailsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        if (focused == null) return;

        DependencyObject? current = focused;
        while (current != null && current is not ListViewItem)
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);

        if (current is not ListViewItem listViewItem) return;

        var dataItem = SettingsListViewControl.ItemFromContainer(listViewItem);
        if (dataItem is not SettingItemViewModel vm) return;

        if (!vm.ShowTechnicalDetailsBar) return;

        vm.ToggleTechnicalDetails();

        var localizationService = App.Services.GetService<ILocalizationService>();
        var stateText = vm.IsTechnicalDetailsExpanded
            ? localizationService.GetStringOrDefault("TechnicalDetails_On", "Technical Details: On")
            : localizationService.GetStringOrDefault("TechnicalDetails_Off", "Technical Details: Off");

        var announcement = $"{vm.Name}: {stateText}";

        if (vm.IsTechnicalDetailsExpanded && vm.TechnicalDetailMatrix is not null)
        {
            var details = string.Join(". ",
                [vm.TechnicalDetailMatrix.AccessibleSummary]);
            announcement = $"{announcement}. {details}";
        }

        // Raise on the focused element so Narrator picks it up
        if (focused is UIElement focusedUi)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(focusedUi)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(focusedUi);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                announcement,
                "TechnicalDetailsToggle");
        }
    }

    private static void OnGroupedSettingsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsListView control)
            control.DeferSource(e.NewValue as ICollectionView);
    }

    // Handed over at once, the old page stays frozen on screen while the first screenful of cards is built (and
    // every card, where the list sits inside a scrolling page). Held back until the page has drawn with the loader,
    // the page appears at once.
    private void DeferSource(ICollectionView? source)
    {
        bool waiting = _framesBeforeSource > 0;
        _pendingSource = source;
        _framesBeforeSource = 2;
        _revealing = source is { Count: > 0 };
        ShowLoadingState();

        if (!waiting)
            CompositionTarget.Rendering += OnRenderingBeforeSource;
    }

    // Rendering fires before a frame is drawn, so the second one means the frame with the loader is on screen.
    private void OnRenderingBeforeSource(object? sender, object e)
    {
        if (--_framesBeforeSource > 0)
            return;

        CompositionTarget.Rendering -= OnRenderingBeforeSource;
        SettingsListViewControl.ItemsSource = _pendingSource;
        SettingsListViewControl.LayoutUpdated -= ListView_LayoutUpdated;
        if (_revealing)
            SettingsListViewControl.LayoutUpdated += ListView_LayoutUpdated;
        ScheduleFocusFirstSetting();
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsListView control)
        {
            // A source still waiting for its frame keeps its own reveal; handing it over hooks the layout check.
            if (control._framesBeforeSource > 0)
            {
                control.ShowLoadingState();
                return;
            }

            control._revealing = e.OldValue is true && e.NewValue is false && control.SettingsListViewControl.Items.Count > 0;
            control.ShowLoadingState();
            control.SettingsListViewControl.LayoutUpdated -= control.ListView_LayoutUpdated;
            if (control._revealing)
                control.SettingsListViewControl.LayoutUpdated += control.ListView_LayoutUpdated;
        }
    }

    private void ShowLoadingState()
    {
        LoadingRocket.IsActive = IsLoading || _revealing;
        SettingsListViewControl.Visibility = IsLoading ? Visibility.Collapsed : Visibility.Visible;
        SettingsListViewControl.Opacity = _revealing ? 0 : 1;
        if (!_scrollerSetUp && !IsLoading && IsLoaded)
            SetUpScroller();
    }

    private void ListView_LayoutUpdated(object? sender, object e)
    {
        if (IsLoading || SettingsListViewControl.ContainerFromIndex(0) is not ListViewItem { ContentTemplateRoot: not null })
            return;

        SettingsListViewControl.LayoutUpdated -= ListView_LayoutUpdated;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            try
            {
                if (ScrollMemoryKey is { } key && await ScrollMemory.RestoreAsync(key, SettingsListViewControl))
                    FocusFirstVisibleSetting();
            }
            finally
            {
                _revealing = false;
                ShowLoadingState();
            }
        });
    }

    // Focus waits on the first card while the list is hidden. After a restored scroll that card is out of view and the
    // first Tab would jump the page back to the top, so focus moves to the first card the restore brought into view,
    // unless the user has already put it somewhere else.
    private void FocusFirstVisibleSetting()
    {
        if (XamlRoot is null || SettingsListViewControl.FindDescendant<ScrollViewer>() is not { } scroller) return;

        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (focused is not null && focused != SettingsListViewControl)
            focused = VisualTreeHelper.GetParent(focused);
        if (focused is null && FocusManager.GetFocusedElement(XamlRoot) is not null) return;

        for (var i = 0; i < SettingsListViewControl.Items.Count; i++)
        {
            if (SettingsListViewControl.ContainerFromIndex(i) is not ListViewItem row
                || row.TransformToVisual(scroller).TransformPoint(default).Y < 0)
                continue;
            if (FocusManager.FindFirstFocusableElement(row) is Control target)
                target.Focus(FocusState.Programmatic);
            return;
        }
    }

    private void ScheduleFocusFirstSetting()
    {
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (SettingsListViewControl.Items.Count > 0)
            {
                var container = SettingsListViewControl.ContainerFromIndex(0) as ListViewItem;
                if (container != null)
                {
                    var firstFocusable = FocusManager.FindFirstFocusableElement(container);
                    if (firstFocusable is Control focusTarget)
                    {
                        focusTarget.Focus(FocusState.Programmatic);
                        return;
                    }
                }
                SettingsListViewControl.Focus(FocusState.Programmatic);
            }
        });
    }
}
