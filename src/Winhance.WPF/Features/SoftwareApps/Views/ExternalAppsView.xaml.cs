using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Winhance.WPF.Features.Common.Utilities;
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
            foreach (var border in VisualTreeHelpers.FindVisualChildren<Border>(this))
            {
                if (border?.Tag != null && border.Tag is string)
                    border.MouseLeftButtonDown += CategoryHeader_MouseLeftButtonDown;
            }
        }

        private void CategoryHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.DataContext is ExternalAppsCategoryViewModel category)
                {
                    category.IsExpanded = !category.IsExpanded;
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling category click: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
