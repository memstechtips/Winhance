using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// A custom ScrollViewer that handles mouse wheel events regardless of cursor position
    /// </summary>
    public class ResponsiveScrollViewer : ScrollViewer
    {
        public ResponsiveScrollViewer()
        {
            // Register for the PreviewMouseWheel event
            this.PreviewMouseWheel += ResponsiveScrollViewer_PreviewMouseWheel;
        }

        /// <summary>
        /// Handles the PreviewMouseWheel event to scroll the content
        /// </summary>
        private void ResponsiveScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Scroll the content based on the mouse wheel delta
            if (e.Delta < 0)
            {
                // Scroll down when the mouse wheel is rotated down
                this.LineDown();
                this.LineDown();
                this.LineDown();
                this.LineDown();
                this.LineDown();
                this.LineDown();
                this.LineDown();
                this.LineDown();
            }
            else
            {
                // Scroll up when the mouse wheel is rotated up
                this.LineUp();
                this.LineUp();
                this.LineUp();
                this.LineUp();
                this.LineUp();
                this.LineUp();
                this.LineUp();
                this.LineUp();
            }

            // Mark the event as handled to prevent it from bubbling up
            e.Handled = true;
        }
    }
}