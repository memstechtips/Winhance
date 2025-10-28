using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    public partial class WindowsAppsHelpContent : UserControl
    {
        public WindowsAppsHelpContent()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        public WindowsAppsHelpContent(WindowsAppsHelpContentViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAllAnimations(this);
        }

        private void StopAllAnimations(DependencyObject parent)
        {
            if (parent == null) return;

            if (parent is UIElement element)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
            }

            if (parent is FrameworkElement frameworkElement && frameworkElement.RenderTransform is RotateTransform rotateTransform)
            {
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                StopAllAnimations(child);
            }
        }
    }
}
