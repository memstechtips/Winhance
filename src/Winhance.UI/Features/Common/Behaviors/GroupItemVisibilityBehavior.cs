using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Common.Utilities;

namespace Winhance.UI.Features.Common.Behaviors;

/// <summary>
/// Attached behavior that manages GroupItem visibility based on whether
/// it has any visible children. Hides empty groups in grouped lists.
/// </summary>
public static class GroupItemVisibilityBehavior
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(GroupItemVisibilityBehavior),
            new PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);
    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GroupItem groupItem && (bool)e.NewValue)
        {
            groupItem.Loaded += (s, args) => UpdateVisibility(groupItem);
            groupItem.LayoutUpdated += (s, args) => UpdateVisibility(groupItem);
        }
    }

    private static void UpdateVisibility(GroupItem groupItem)
    {
        var itemsPresenter = VisualTreeHelpers.FindVisualChild<ItemsPresenter>(groupItem);
        if (itemsPresenter == null) return;

        var panel = VisualTreeHelpers.FindVisualChild<Panel>(itemsPresenter);
        if (panel == null) return;

        bool hasVisibleChildren = false;
        foreach (var child in panel.Children)
        {
            if (child.Visibility == Visibility.Visible)
            {
                hasVisibleChildren = true;
                break;
            }
        }

        groupItem.Visibility = hasVisibleChildren ? Visibility.Visible : Visibility.Collapsed;
    }
}
