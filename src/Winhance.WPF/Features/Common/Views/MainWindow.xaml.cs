using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// MainWindow - responsible ONLY for view initialization
    /// All business logic is handled by MainViewModel and services
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Only essential UI event handlers remain in the View
            this.PreviewMouseWheel += MainWindow_PreviewMouseWheel;
        }

        /// <summary>
        /// Essential UI helper method - finds visual children in the tree
        /// This is pure UI functionality and can remain in the View
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject obj)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                if (child != null && child is T)
                {
                    return (T)child;
                }

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }

            return null;
        }

        /// <summary>
        /// Essential UI event handler - handles mouse wheel scrolling
        /// This is pure UI behavior and can remain in the View
        /// </summary>
        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(this);
            if (scrollViewer != null)
            {
                // Redirect the mouse wheel event to the ScrollViewer
                if (e.Delta < 0)
                {
                    scrollViewer.LineDown();
                }
                else
                {
                    scrollViewer.LineUp();
                }

                // Mark the event as handled to prevent it from bubbling up
                e.Handled = true;
            }
        }
    }
}
