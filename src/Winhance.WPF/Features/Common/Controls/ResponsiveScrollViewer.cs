using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// A custom ScrollViewer that handles mouse wheel events regardless of cursor position
    /// and provides enhanced scrolling speed
    /// </summary>
    public class ResponsiveScrollViewer : ScrollViewer
    {
        #region Dependency Properties

        /// <summary>
        /// Dependency property for ScrollSpeedMultiplier
        /// </summary>
        public static readonly DependencyProperty ScrollSpeedMultiplierProperty =
            DependencyProperty.RegisterAttached(
                "ScrollSpeedMultiplier",
                typeof(double),
                typeof(ResponsiveScrollViewer),
                new PropertyMetadata(10.0));

        /// <summary>
        /// Gets the scroll speed multiplier for a ScrollViewer
        /// </summary>
        public static double GetScrollSpeedMultiplier(DependencyObject obj)
        {
            return (double)obj.GetValue(ScrollSpeedMultiplierProperty);
        }

        /// <summary>
        /// Sets the scroll speed multiplier for a ScrollViewer
        /// </summary>
        public static void SetScrollSpeedMultiplier(DependencyObject obj, double value)
        {
            obj.SetValue(ScrollSpeedMultiplierProperty, value);
        }

        #endregion

        /// <summary>
        /// Static constructor to register event handlers for all ScrollViewers
        /// </summary>
        static ResponsiveScrollViewer()
        {
            // Register class handler for the PreviewMouseWheel event
            EventManager.RegisterClassHandler(
                typeof(ScrollViewer),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnPreviewMouseWheel),
                true);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public ResponsiveScrollViewer()
        {
            // No need to register for the event here anymore as we're using a class handler
        }

        /// <summary>
        /// Handles the PreviewMouseWheel event for all ScrollViewers
        /// </summary>
        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // Get the current vertical offset
                double currentOffset = scrollViewer.VerticalOffset;

                // Get the scroll speed multiplier (use default value if not set)
                double speedMultiplier = GetScrollSpeedMultiplier(scrollViewer);

                // Calculate the scroll amount based on the mouse wheel delta and speed multiplier
                double scrollAmount = (SystemParameters.WheelScrollLines * speedMultiplier);

                if (e.Delta < 0)
                {
                    // Scroll down when the mouse wheel is rotated down
                    scrollViewer.ScrollToVerticalOffset(currentOffset + scrollAmount);
                }
                else
                {
                    // Scroll up when the mouse wheel is rotated up
                    scrollViewer.ScrollToVerticalOffset(currentOffset - scrollAmount);
                }

                // Mark the event as handled to prevent it from bubbling up
                e.Handled = true;
            }
        }
    }
}
