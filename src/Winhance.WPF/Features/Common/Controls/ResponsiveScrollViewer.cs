using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Winhance.WPF.Features.Common.Controls
{
    public class ResponsiveScrollViewer : ScrollViewer
    {
        private bool _isScrollChangedSubscribed = false;

        public static readonly DependencyProperty ScrollSpeedMultiplierProperty =
            DependencyProperty.RegisterAttached(
                "ScrollSpeedMultiplier",
                typeof(double),
                typeof(ResponsiveScrollViewer),
                new PropertyMetadata(10.0));

        public static readonly DependencyProperty ScrollPositionCommandProperty =
            DependencyProperty.RegisterAttached(
                "ScrollPositionCommand",
                typeof(ICommand),
                typeof(ResponsiveScrollViewer),
                new PropertyMetadata(null));

        public static double GetScrollSpeedMultiplier(DependencyObject obj)
        {
            return (double)obj.GetValue(ScrollSpeedMultiplierProperty);
        }

        public static void SetScrollSpeedMultiplier(DependencyObject obj, double value)
        {
            obj.SetValue(ScrollSpeedMultiplierProperty, value);
        }

        public static ICommand GetScrollPositionCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(ScrollPositionCommandProperty);
        }

        public static void SetScrollPositionCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(ScrollPositionCommandProperty, value);
        }

        static ResponsiveScrollViewer()
        {
            EventManager.RegisterClassHandler(
                typeof(ScrollViewer),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnPreviewMouseWheel),
                true);
        }

        public ResponsiveScrollViewer()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isScrollChangedSubscribed)
            {
                ScrollChanged += OnScrollChanged;
                _isScrollChangedSubscribed = true;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_isScrollChangedSubscribed)
            {
                ScrollChanged -= OnScrollChanged;
                _isScrollChangedSubscribed = false;
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var command = GetScrollPositionCommand(this);
            if (command?.CanExecute(e.VerticalOffset) == true)
            {
                command.Execute(e.VerticalOffset);
            }
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                if (!IsEventSourceInScrollViewer(scrollViewer, e.OriginalSource as DependencyObject))
                {
                    return;
                }

                double currentOffset = scrollViewer.VerticalOffset;
                double speedMultiplier = GetScrollSpeedMultiplier(scrollViewer);
                double scrollAmount = (SystemParameters.WheelScrollLines * speedMultiplier);

                if (e.Delta < 0)
                {
                    scrollViewer.ScrollToVerticalOffset(currentOffset + scrollAmount);
                }
                else
                {
                    scrollViewer.ScrollToVerticalOffset(currentOffset - scrollAmount);
                }

                e.Handled = true;
            }
        }

        private static bool IsEventSourceInScrollViewer(ScrollViewer scrollViewer, DependencyObject source)
        {
            if (source == null) return false;

            DependencyObject current = source;
            while (current != null)
            {
                if (current == scrollViewer)
                    return true;

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                if (current == null)
                {
                    break;
                }
            }
            return false;
        }
    }
}
