using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Custom ComboBox control for Power Plan selection with status indicators,
/// [Active] badges, and delete functionality.
/// </summary>
public sealed partial class PowerPlanComboBox : UserControl
{
    // Cached brushes for status indicators
    private static readonly SolidColorBrush ExistsBrush = new(Color.FromArgb(255, 0, 200, 60));
    private static readonly SolidColorBrush NotExistsBrush = new(Color.FromArgb(255, 200, 40, 0));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<ComboBoxOption>),
            typeof(PowerPlanComboBox),
            new PropertyMetadata(null, OnItemsSourceChanged));

    // Tracks the last CollectionChanged handler so we can unsubscribe when the collection changes
    private NotifyCollectionChangedEventHandler? _collectionChangedHandler;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PowerPlanComboBox control) return;

        // Unsubscribe from the old collection
        if (e.OldValue is ObservableCollection<ComboBoxOption> oldCollection && control._collectionChangedHandler != null)
        {
            oldCollection.CollectionChanged -= control._collectionChangedHandler;
            control._collectionChangedHandler = null;
        }

        if (e.NewValue is ObservableCollection<ComboBoxOption> newCollection)
        {
            LogDebug($"[PowerPlanComboBox] ItemsSourceProperty changed, count={newCollection.Count}");

            // Debounced handler: only re-apply SelectedValue once after all items are added,
            // instead of on every individual Add (which caused redundant deferred tasks during refresh)
            DispatcherQueueTimer? debounceTimer = null;

            control._collectionChangedHandler = (s, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    debounceTimer?.Stop();
                    debounceTimer = control.DispatcherQueue.CreateTimer();
                    debounceTimer.Interval = TimeSpan.FromMilliseconds(50);
                    debounceTimer.IsRepeating = false;
                    debounceTimer.Tick += (t, _) =>
                    {
                        debounceTimer.Stop();
                        if (control.SelectedValue != null && control.PowerPlanSelector != null)
                        {
                            LogDebug($"[PowerPlanComboBox] Debounced: Re-applying SelectedValue={control.SelectedValue}");
                            control.PowerPlanSelector.SelectedValue = control.SelectedValue;
                        }
                    };
                    debounceTimer.Start();
                }
            };

            newCollection.CollectionChanged += control._collectionChangedHandler;
        }
    }

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(
            nameof(SelectedValue),
            typeof(object),
            typeof(PowerPlanComboBox),
            new PropertyMetadata(null, OnSelectedValueChanged));

    private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PowerPlanComboBox control)
        {
            LogDebug($"[PowerPlanComboBox] SelectedValueProperty changed: old={e.OldValue}, new={e.NewValue}");
            // Sync the inner ComboBox - defer to allow ItemsSource binding to update first
            if (control.PowerPlanSelector != null)
            {
                var newValue = e.NewValue;
                LogDebug($"[PowerPlanComboBox] Deferring PowerPlanSelector.SelectedValue to {newValue}");
                control.DispatcherQueue.TryEnqueue(() =>
                {
                    LogDebug($"[PowerPlanComboBox] Deferred: Setting PowerPlanSelector.SelectedValue to {newValue}");
                    control.PowerPlanSelector.SelectedValue = newValue;
                    LogDebug($"[PowerPlanComboBox] Deferred: PowerPlanSelector.SelectedValue is now {control.PowerPlanSelector.SelectedValue}");
                });
            }
        }
    }

    public static readonly DependencyProperty ActiveBadgeTextProperty =
        DependencyProperty.Register(
            nameof(ActiveBadgeText),
            typeof(string),
            typeof(PowerPlanComboBox),
            new PropertyMetadata("[Active]"));

    public static readonly DependencyProperty DeleteTooltipTextProperty =
        DependencyProperty.Register(
            nameof(DeleteTooltipText),
            typeof(string),
            typeof(PowerPlanComboBox),
            new PropertyMetadata("Delete this power plan"));

    public static readonly DependencyProperty ExistsTooltipTextProperty =
        DependencyProperty.Register(
            nameof(ExistsTooltipText),
            typeof(string),
            typeof(PowerPlanComboBox),
            new PropertyMetadata("Installed on system"));

    public static readonly DependencyProperty NotExistsTooltipTextProperty =
        DependencyProperty.Register(
            nameof(NotExistsTooltipText),
            typeof(string),
            typeof(PowerPlanComboBox),
            new PropertyMetadata("Predefined plan (click to install)"));

    public ObservableCollection<ComboBoxOption>? ItemsSource
    {
        get => (ObservableCollection<ComboBoxOption>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public string ActiveBadgeText
    {
        get => (string)GetValue(ActiveBadgeTextProperty);
        set => SetValue(ActiveBadgeTextProperty, value);
    }

    public string DeleteTooltipText
    {
        get => (string)GetValue(DeleteTooltipTextProperty);
        set => SetValue(DeleteTooltipTextProperty, value);
    }

    public string ExistsTooltipText
    {
        get => (string)GetValue(ExistsTooltipTextProperty);
        set => SetValue(ExistsTooltipTextProperty, value);
    }

    public string NotExistsTooltipText
    {
        get => (string)GetValue(NotExistsTooltipTextProperty);
        set => SetValue(NotExistsTooltipTextProperty, value);
    }

    /// <summary>
    /// Event raised when the delete button is clicked for a power plan.
    /// </summary>
    public event EventHandler<PowerPlanComboBoxOption>? DeleteRequested;

    /// <summary>
    /// Event raised when the dropdown is closed (selection changed).
    /// </summary>
    public event EventHandler<object>? DropDownClosed;

    public PowerPlanComboBox()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Handles the dropdown opened event to set up visual states for all items.
    /// </summary>
    private void OnDropDownOpened(object sender, object e)
    {
        // Use DispatcherQueue to ensure the visual tree is ready
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateAllItemVisualStates();
        });
    }

    /// <summary>
    /// Updates the visual state of all items in the ComboBox.
    /// </summary>
    private void UpdateAllItemVisualStates()
    {
        if (ItemsSource == null) return;

        for (int i = 0; i < ItemsSource.Count; i++)
        {
            var container = PowerPlanSelector.ContainerFromIndex(i) as ComboBoxItem;
            if (container == null) continue;

            var option = ItemsSource[i];
            var powerPlanOption = option.Tag as PowerPlanComboBoxOption;
            if (powerPlanOption == null) continue;

            // Find the Grid in the item template
            var grid = FindChild<Grid>(container, null);
            if (grid == null) continue;

            SetupItemVisualState(grid, powerPlanOption, option.Tag);
        }
    }

    /// <summary>
    /// Sets up the visual state for a single item.
    /// </summary>
    private void SetupItemVisualState(Grid grid, PowerPlanComboBoxOption powerPlanOption, object? tag)
    {
        // Find child elements
        var statusIndicator = FindChild<Ellipse>(grid, "StatusIndicator");
        var activeBadge = FindChild<TextBlock>(grid, "ActiveBadge");
        var deleteButton = FindChild<Button>(grid, "DeleteButton");

        // Set status indicator color and tooltip
        if (statusIndicator != null)
        {
            statusIndicator.Fill = powerPlanOption.ExistsOnSystem ? ExistsBrush : NotExistsBrush;
            ToolTipService.SetToolTip(statusIndicator,
                powerPlanOption.ExistsOnSystem ? ExistsTooltipText : NotExistsTooltipText);
        }

        // Set [Active] badge visibility and text
        if (activeBadge != null)
        {
            activeBadge.Visibility = powerPlanOption.IsActive ? Visibility.Visible : Visibility.Collapsed;
            activeBadge.Text = ActiveBadgeText;
        }

        // Set delete button visibility: visible only when exists AND not active
        if (deleteButton != null)
        {
            deleteButton.Visibility = (powerPlanOption.ExistsOnSystem && !powerPlanOption.IsActive)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ToolTipService.SetToolTip(deleteButton, DeleteTooltipText);

            // Wire up the click handler and set the tag for identifying the plan
            deleteButton.Tag = powerPlanOption;
            deleteButton.Click -= OnDeleteButtonClick;
            deleteButton.Click += OnDeleteButtonClick;
        }
    }

    /// <summary>
    /// Handles the delete button click event.
    /// </summary>
    private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PowerPlanComboBoxOption option)
        {
            DeleteRequested?.Invoke(this, option);
        }
    }

    /// <summary>
    /// Handles the dropdown closed event to propagate selection changes.
    /// </summary>
    private void OnDropDownClosed(object sender, object e)
    {
        LogDebug($"[PowerPlanComboBox] OnDropDownClosed fired, SelectedValue={PowerPlanSelector.SelectedValue}");
        if (PowerPlanSelector.SelectedValue is { } value)
        {
            LogDebug($"[PowerPlanComboBox] Invoking DropDownClosed event with value={value}");
            DropDownClosed?.Invoke(this, value);
        }
        else
        {
            LogDebug($"[PowerPlanComboBox] SelectedValue is null, not invoking event");
        }
    }

    private static void LogDebug(string message)
    {
        try { App.Services.GetService<ILogService>()?.LogDebug(message); } catch { }
    }

    /// <summary>
    /// Helper method to find a child element by name within a parent element.
    /// If name is null, returns the first child of the specified type.
    /// </summary>
    private static T? FindChild<T>(DependencyObject parent, string? childName) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
            {
                if (childName == null || typedChild.Name == childName)
                {
                    return typedChild;
                }
            }

            var result = FindChild<T>(child, childName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }
}
