using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Winhance.Core.Features.Common.Models;
using ComboBoxDisplayOption = Winhance.Core.Features.Common.Interfaces.ComboBoxDisplayOption;
using VirtualKey = Windows.System.VirtualKey;

namespace Winhance.UI.Features.Common.Controls;

public sealed partial class PowerPlanComboBox : UserControl
{
    private static readonly SolidColorBrush ExistsBrush = new(Color.FromArgb(255, 0, 200, 60));
    private static readonly SolidColorBrush NotExistsBrush = new(Color.FromArgb(255, 200, 40, 0));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<ComboBoxDisplayOption>),
            typeof(PowerPlanComboBox),
            new PropertyMetadata(null, OnItemsSourceChanged));

    private NotifyCollectionChangedEventHandler? _collectionChangedHandler;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PowerPlanComboBox control) return;

        if (e.OldValue is ObservableCollection<ComboBoxDisplayOption> oldCollection && control._collectionChangedHandler != null)
        {
            oldCollection.CollectionChanged -= control._collectionChangedHandler;
            control._collectionChangedHandler = null;
        }

        if (e.NewValue is ObservableCollection<ComboBoxDisplayOption> newCollection)
        {
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
            // Sync the inner ComboBox - defer to allow ItemsSource binding to update first
            if (control.PowerPlanSelector != null)
            {
                var newValue = e.NewValue;
                control.DispatcherQueue.TryEnqueue(() =>
                {
                    control.PowerPlanSelector.SelectedValue = newValue;
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

    public ObservableCollection<ComboBoxDisplayOption>? ItemsSource
    {
        get => (ObservableCollection<ComboBoxDisplayOption>?)GetValue(ItemsSourceProperty);
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

    public event EventHandler<PowerPlanComboBoxOption>? DeleteRequested;

    public event EventHandler<object>? DropDownClosed;

    public PowerPlanComboBox()
    {
        this.InitializeComponent();
    }

    private void OnDropDownOpened(object sender, object e)
    {
        // Use DispatcherQueue to ensure the visual tree is ready
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateAllItemVisualStates();
        });
    }

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

            var grid = FindChild<Grid>(container, null);
            if (grid == null) continue;

            SetupItemVisualState(grid, powerPlanOption);

            // Keyboard accessibility: Delete key on the focused item triggers DeleteRequested.
            // The trash button itself can't be tab-reached inside an open ComboBox (Tab dismisses
            // the dropdown), so this is the standard Windows list-deletion convention.
            container.Tag = powerPlanOption;
            container.KeyDown -= OnItemKeyDown;
            container.KeyDown += OnItemKeyDown;
        }
    }

    // Matches the visibility gate of the trash icon: only plans that exist on the system and aren't active.
    private void OnItemKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Delete
            && sender is ComboBoxItem container
            && container.Tag is PowerPlanComboBoxOption option
            && option.ExistsOnSystem
            && !option.IsActive)
        {
            DeleteRequested?.Invoke(this, option);
            e.Handled = true;
        }
    }

    private void SetupItemVisualState(Grid grid, PowerPlanComboBoxOption powerPlanOption)
    {
        var statusIndicator = FindChild<Ellipse>(grid, "StatusIndicator");
        var activeBadge = FindChild<TextBlock>(grid, "ActiveBadge");
        var deleteButton = FindChild<Button>(grid, "DeleteButton");

        if (statusIndicator != null)
        {
            statusIndicator.Fill = powerPlanOption.ExistsOnSystem ? ExistsBrush : NotExistsBrush;
            ToolTipService.SetToolTip(statusIndicator,
                powerPlanOption.ExistsOnSystem ? ExistsTooltipText : NotExistsTooltipText);
        }

        if (activeBadge != null)
        {
            activeBadge.Visibility = powerPlanOption.IsActive ? Visibility.Visible : Visibility.Collapsed;
            activeBadge.Text = ActiveBadgeText;
        }

        if (deleteButton != null)
        {
            deleteButton.Visibility = (powerPlanOption.ExistsOnSystem && !powerPlanOption.IsActive)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ToolTipService.SetToolTip(deleteButton, DeleteTooltipText);
            AutomationProperties.SetName(deleteButton, DeleteTooltipText);

            deleteButton.Tag = powerPlanOption;
            deleteButton.Click -= OnDeleteButtonClick;
            deleteButton.Click += OnDeleteButtonClick;
        }
    }

    private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PowerPlanComboBoxOption option)
        {
            DeleteRequested?.Invoke(this, option);
        }
    }

    private void OnDropDownClosed(object sender, object e)
    {
        if (PowerPlanSelector.SelectedValue is { } value)
        {
            DropDownClosed?.Invoke(this, value);
        }
    }

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
