using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    public partial class ExternalAppsView : UserControl
    {
        public ExternalAppsView()
        {
            InitializeComponent();
            Loaded += ExternalAppsView_Loaded;
        }

        private void ExternalAppsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Find all category header borders and attach click handlers
            foreach (var border in FindVisualChildren<Border>(this))
            {
                if (border?.Tag != null && border.Tag is string)
                {
                    // Add click handler to toggle category expansion
                    border.MouseLeftButtonDown += CategoryHeader_MouseLeftButtonDown;
                }
            }
        }

        private void CategoryHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.DataContext is ExternalAppsCategoryViewModel category)
                {
                    // Toggle the IsExpanded property
                    category.IsExpanded = !category.IsExpanded;
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling category click: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper method to find visual children of a specific type
        private System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T childOfType)
                    yield return childOfType;
                    
                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }
    }
}
