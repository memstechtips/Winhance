using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Winhance.WPF.Features.Common.Behaviors
{
    public static class ComboBoxDropdownBehavior
    {
        public static readonly DependencyProperty StayWithParentProperty =
            DependencyProperty.RegisterAttached(
                "StayWithParent",
                typeof(bool),
                typeof(ComboBoxDropdownBehavior),
                new PropertyMetadata(false, OnStayWithParentChanged));

        private static readonly DependencyProperty IsHandlingDropdownProperty =
            DependencyProperty.RegisterAttached(
                "IsHandlingDropdown",
                typeof(bool),
                typeof(ComboBoxDropdownBehavior),
                new PropertyMetadata(false));

        private static readonly DependencyProperty OriginalPositionProperty =
            DependencyProperty.RegisterAttached(
                "OriginalPosition",
                typeof(Point?),
                typeof(ComboBoxDropdownBehavior),
                new PropertyMetadata(null));

        public static bool GetStayWithParent(DependencyObject obj)
        {
            return (bool)obj.GetValue(StayWithParentProperty);
        }

        public static void SetStayWithParent(DependencyObject obj, bool value)
        {
            obj.SetValue(StayWithParentProperty, value);
        }
        private static void OnStayWithParentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ComboBox comboBox)
            {
                if ((bool)e.NewValue)
                {
                    comboBox.DropDownOpened += ComboBox_DropDownOpened;
                    comboBox.DropDownClosed += ComboBox_DropDownClosed;

                    ScrollViewer scrollViewer = FindParentScrollViewer(comboBox);
                    if (scrollViewer != null)
                    {
                        scrollViewer.ScrollChanged += (s, args) => ScrollViewer_ScrollChanged(args, comboBox);
                    }
                }
                else
                {
                    comboBox.DropDownOpened -= ComboBox_DropDownOpened;
                    comboBox.DropDownClosed -= ComboBox_DropDownClosed;
                }
            }
        }

        private static void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                try
                {
                    Point position = comboBox.PointToScreen(new Point(0, 0));
                    comboBox.SetValue(OriginalPositionProperty, position);
                }
                catch
                {
                    comboBox.SetValue(OriginalPositionProperty, null);
                }

                if (comboBox.Template.FindName("Popup", comboBox) is Popup popup)
                {
                    popup.Placement = PlacementMode.Bottom;
                    popup.PlacementTarget = comboBox;

                    if (popup.Child != null)
                    {
                        popup.Child.PreviewMouseWheel += PopupChild_PreviewMouseWheel;
                    }
                }
            }
        }

        private static void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.SetValue(OriginalPositionProperty, null);

                if (comboBox.Template.FindName("Popup", comboBox) is Popup popup)
                {
                    if (popup.Child != null)
                    {
                        popup.Child.PreviewMouseWheel -= PopupChild_PreviewMouseWheel;
                    }
                }
            }
        }

        private static void PopupChild_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DependencyObject element)
            {
                ScrollViewer scrollViewer = FindScrollViewer(element);
                if (scrollViewer != null)
                {
                    double scrollAmount = SystemParameters.WheelScrollLines * 16;

                    if (e.Delta > 0)
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollAmount);
                    }
                    else
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);
                    }

                    e.Handled = true;
                }
            }
        }
        private static void ScrollViewer_ScrollChanged(ScrollChangedEventArgs args, ComboBox comboBox)
        {
            if (!comboBox.IsDropDownOpen)
                return;

            if (args.VerticalChange == 0 && args.HorizontalChange == 0)
                return;

            Point? originalPosition = (Point?)comboBox.GetValue(OriginalPositionProperty);
            if (!originalPosition.HasValue)
                return;

            try
            {
                Point currentPosition = comboBox.PointToScreen(new Point(0, 0));
                double distance = Math.Sqrt(
                    Math.Pow(currentPosition.X - originalPosition.Value.X, 2) +
                    Math.Pow(currentPosition.Y - originalPosition.Value.Y, 2));

                if (distance > 5)
                {
                    comboBox.IsDropDownOpen = false;
                }
            }
            catch
            {
                comboBox.IsDropDownOpen = false;
            }
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            if (child == null || parent == null)
                return false;

            if (child == parent)
                return true;

            DependencyObject currentParent = VisualTreeHelper.GetParent(child);
            while (currentParent != null)
            {
                if (currentParent == parent)
                    return true;
                currentParent = VisualTreeHelper.GetParent(currentParent);
            }

            return false;
        }

        private static bool IsMouseOver(UIElement element)
        {
            Point mousePos = Mouse.GetPosition(element);
            return mousePos.X >= 0 && mousePos.X <= element.RenderSize.Width &&
                   mousePos.Y >= 0 && mousePos.Y <= element.RenderSize.Height;
        }
        private static ScrollViewer FindParentScrollViewer(DependencyObject child)
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is ScrollViewer parent)
                return parent;
            else
                return FindParentScrollViewer(parentObject);
        }

        private static ScrollViewer FindScrollViewer(DependencyObject element)
        {
            if (element == null) return null;

            if (element is ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                ScrollViewer result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
