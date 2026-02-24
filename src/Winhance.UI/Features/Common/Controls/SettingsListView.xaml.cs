using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

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

    public bool IsNotLoading => !IsLoading;

    public bool HasNoSearchResults
    {
        get => (bool)GetValue(HasNoSearchResultsProperty);
        set => SetValue(HasNoSearchResultsProperty, value);
    }

    public SettingsListView()
    {
        this.InitializeComponent();
        SettingsListViewControl.LosingFocus += ListView_LosingFocus;
    }

    /// <summary>
    /// Intercepts Tab focus leaving the ListView and redirects it to the next/previous
    /// setting control within the list. The ListView's built-in TabFocusNavigation=Once
    /// forces Tab to exit after one stop — this handler overrides that behavior.
    /// </summary>
    private void ListView_LosingFocus(object sender, LosingFocusEventArgs e)
    {
        if (e.InputDevice != FocusInputDeviceKind.Keyboard) return;

        // Determine the navigation direction:
        // Direction=Next → Tab forward, Direction=Previous → Shift+Tab backward,
        // Direction=None with New=null → ListView couldn't find next item (treat as forward)
        bool isForward;
        if (e.Direction == FocusNavigationDirection.Next)
            isForward = true;
        else if (e.Direction == FocusNavigationDirection.Previous)
            isForward = false;
        else if (e.Direction == FocusNavigationDirection.None && e.NewFocusedElement == null)
            isForward = true;
        else
            return; // Arrow key navigation or other — don't intercept

        // Find which ListViewItem the currently focused element is inside
        var oldElement = e.OldFocusedElement as DependencyObject;
        if (oldElement == null) return;

        DependencyObject? current = oldElement;
        while (current != null && current is not ListViewItem)
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);

        if (current is not ListViewItem currentItem) return;

        // If focus is moving within the same ListViewItem, don't intercept
        if (e.NewFocusedElement is DependencyObject newElement)
        {
            DependencyObject? newParent = newElement;
            while (newParent != null && newParent is not ListViewItem)
                newParent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(newParent);
            if (newParent == currentItem) return;
        }

        var currentIndex = SettingsListViewControl.IndexFromContainer(currentItem);
        if (currentIndex < 0) return;

        // Search for the next/previous item that has a focusable control
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

    /// <summary>
    /// Handles Ctrl+D to toggle Technical Details for the currently focused setting.
    /// </summary>
    private void TechnicalDetailsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        if (focused == null) return;

        // Walk up to find the ListViewItem containing the focused element
        DependencyObject? current = focused;
        while (current != null && current is not ListViewItem)
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);

        if (current is not ListViewItem listViewItem) return;

        // Get the SettingItemViewModel from the item's data context
        var dataItem = SettingsListViewControl.ItemFromContainer(listViewItem);
        if (dataItem is not SettingItemViewModel vm) return;

        // Only toggle if the setting has technical details and they're globally visible
        if (!vm.ShowTechnicalDetailsBar) return;

        vm.ToggleTechnicalDetails();

        // Announce the new state and details content to Narrator
        var localizationService = App.Services.GetService<ILocalizationService>();
        var stateText = vm.IsTechnicalDetailsExpanded
            ? localizationService?.GetString("TechnicalDetails_On") ?? "Technical Details: On"
            : localizationService?.GetString("TechnicalDetails_Off") ?? "Technical Details: Off";

        var announcement = $"{vm.Name}: {stateText}";

        // When expanding, append the technical details content so Narrator reads it
        if (vm.IsTechnicalDetailsExpanded && vm.TechnicalDetails.Count > 0)
        {
            var details = string.Join(". ", vm.TechnicalDetails.Select(d => d.AccessibleSummary));
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
        {
            control.SettingsListViewControl.ItemsSource = e.NewValue as ICollectionView;
            control.ScheduleFocusFirstSetting();
        }
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsListView control)
        {
            control.Bindings.Update();
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
