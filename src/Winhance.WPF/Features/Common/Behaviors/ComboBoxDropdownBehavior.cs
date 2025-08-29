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
    /// <summary>
    /// Attached behavior that ensures ComboBox dropdowns stay with their parent when scrolling
    /// </summary>
    public static class ComboBoxDropdownBehavior
    {
        // Dependency property to enable the behavior
        public static readonly DependencyProperty StayWithParentProperty =
            DependencyProperty.RegisterAttached(
                "StayWithParent",
                typeof(bool),
                typeof(ComboBoxDropdownBehavior),
                new PropertyMetadata(false, OnStayWithParentChanged));

        // Property to track if we're handling the dropdown
        private static readonly DependencyProperty IsHandlingDropdownProperty =
            DependencyProperty.RegisterAttached(
                "IsHandlingDropdown",
                typeof(bool),
                typeof(ComboBoxDropdownBehavior),
                new PropertyMetadata(false));

        // Property to store the original position of the ComboBox
        private static readonly DependencyProperty OriginalPositionProperty =
            DependencyProperty.RegisterAttached(
                "OriginalPosition",
                typeof(Point?),
                typeof(ComboBoxDropdownBehavior),
                new PropertyMetadata(null));

        // Getter for StayWithParent property
        public static bool GetStayWithParent(DependencyObject obj)
        {
            return (bool)obj.GetValue(StayWithParentProperty);
        }

        // Setter for StayWithParent property
        public static void SetStayWithParent(DependencyObject obj, bool value)
        {
            obj.SetValue(StayWithParentProperty, value);
        }

        // Handler for when StayWithParent property changes
        private static void OnStayWithParentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ComboBox comboBox)
            {
                if ((bool)e.NewValue)
                {
                    // Attach event handlers when behavior is enabled
                    comboBox.DropDownOpened += ComboBox_DropDownOpened;
                    comboBox.DropDownClosed += ComboBox_DropDownClosed;

                    // Find parent ScrollViewer and attach scroll handler
                    ScrollViewer scrollViewer = FindParentScrollViewer(comboBox);
                    if (scrollViewer != null)
                    {
                        scrollViewer.ScrollChanged += (s, args) => ScrollViewer_ScrollChanged(args, comboBox);
                    }
                }
                else
                {
                    // Detach event handlers when behavior is disabled
                    comboBox.DropDownOpened -= ComboBox_DropDownOpened;
                    comboBox.DropDownClosed -= ComboBox_DropDownClosed;
                }
            }
        }

        // Handler for when dropdown is opened
        private static void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                // Store the original position of the ComboBox when dropdown opens
                try
                {
                    Point position = comboBox.PointToScreen(new Point(0, 0));
                    comboBox.SetValue(OriginalPositionProperty, position);
                }
                catch
                {
                    // If we can't get the position, set it to null
                    comboBox.SetValue(OriginalPositionProperty, null);
                }

                // Find the popup in the ComboBox template
                if (comboBox.Template.FindName("Popup", comboBox) is Popup popup)
                {
                    // Set the popup to use bottom placement
                    popup.Placement = PlacementMode.Bottom;
                    popup.PlacementTarget = comboBox;

                    // Handle the Loaded event to attach mouse wheel handling after popup is fully loaded
                    popup.Loaded += (s, args) => AttachMouseWheelHandling(popup);
                }
            }
        }

        // Handler for when dropdown is closed
        private static void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                // Clear the stored position
                comboBox.SetValue(OriginalPositionProperty, null);

                // Remove mouse wheel handling from the popup
                if (comboBox.Template.FindName("Popup", comboBox) is Popup popup)
                {
                    DetachMouseWheelHandling(popup);
                }
            }
        }

        // Attach mouse wheel handling to the popup content
        private static void AttachMouseWheelHandling(Popup popup)
        {
            if (popup.Child is UIElement popupChild)
            {
                popupChild.PreviewMouseWheel += PopupChild_PreviewMouseWheel;
            }
        }

        // Detach mouse wheel handling from the popup content
        private static void DetachMouseWheelHandling(Popup popup)
        {
            if (popup.Child is UIElement popupChild)
            {
                popupChild.PreviewMouseWheel -= PopupChild_PreviewMouseWheel;
            }
        }

        // Handler for mouse wheel events on the popup child
        private static void PopupChild_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is UIElement popupChild)
            {
                // Find the ScrollViewer within the popup
                ScrollViewer scrollViewer = FindChildScrollViewer(popupChild);
                if (scrollViewer != null)
                {
                    // Manually scroll the ScrollViewer
                    double scrollAmount = SystemParameters.WheelScrollLines * 16; // 16 pixels per line

                    if (e.Delta > 0)
                    {
                        // Scroll up
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollAmount);
                    }
                    else
                    {
                        // Scroll down
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);
                    }

                    // Mark the event as handled to prevent bubbling
                    e.Handled = true;
                }
            }
        }

        // Handler for when ScrollViewer scrolls
        private static void ScrollViewer_ScrollChanged(ScrollChangedEventArgs args, ComboBox comboBox)
        {
            // Only handle if the dropdown is open and we have a stored position
            if (!comboBox.IsDropDownOpen)
                return;

            // Only close if there was actual scrolling (not just size changes)
            if (args.VerticalChange == 0 && args.HorizontalChange == 0)
                return;

            // Get the stored original position
            Point? originalPosition = (Point?)comboBox.GetValue(OriginalPositionProperty);
            if (!originalPosition.HasValue)
                return;

            try
            {
                // Get current position
                Point currentPosition = comboBox.PointToScreen(new Point(0, 0));

                // Calculate the distance moved
                double distance = Math.Sqrt(
                    Math.Pow(currentPosition.X - originalPosition.Value.X, 2) +
                    Math.Pow(currentPosition.Y - originalPosition.Value.Y, 2));

                // If the ComboBox has moved more than a small threshold, close the dropdown
                if (distance > 5) // 5 pixels threshold
                {
                    comboBox.IsDropDownOpen = false;
                }
            }
            catch
            {
                // If we can't determine position, close the dropdown to be safe
                comboBox.IsDropDownOpen = false;
            }
        }

        // Helper method to check if an element is a descendant of another element
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

        // Helper method to check if the mouse is over an element
        private static bool IsMouseOver(UIElement element)
        {
            Point mousePos = Mouse.GetPosition(element);
            return mousePos.X >= 0 && mousePos.X <= element.RenderSize.Width &&
                   mousePos.Y >= 0 && mousePos.Y <= element.RenderSize.Height;
        }

        // Helper method to find parent ScrollViewer
        private static ScrollViewer FindParentScrollViewer(DependencyObject child)
        {
            // Get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            // We've reached the end of the tree
            if (parentObject == null) return null;

            // Check if the parent is a ScrollViewer
            if (parentObject is ScrollViewer parent)
                return parent;
            else
                return FindParentScrollViewer(parentObject);
        }

        // Helper method to find child ScrollViewer
        private static ScrollViewer FindChildScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                // Check for standard ScrollViewer
                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                // Check for ResponsiveScrollViewer (custom control)
                if (child.GetType().Name == "ResponsiveScrollViewer")
                {
                    // ResponsiveScrollViewer should inherit from ScrollViewer or contain one
                    if (child is ScrollViewer responsiveScrollViewer)
                        return responsiveScrollViewer;

                    // If it doesn't inherit from ScrollViewer, look for a ScrollViewer inside it
                    ScrollViewer innerScrollViewer = FindChildScrollViewer(child);
                    if (innerScrollViewer != null)
                        return innerScrollViewer;
                }

                ScrollViewer result = FindChildScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
