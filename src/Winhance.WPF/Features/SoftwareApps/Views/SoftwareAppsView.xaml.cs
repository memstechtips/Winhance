using System.Windows;
using System.Windows.Controls;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    /// <summary>
    /// Interaction logic for SoftwareAppsView.xaml
    /// </summary>
    public partial class SoftwareAppsView : UserControl
    {
        public SoftwareAppsView()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// Event handler for the Help button loaded event
        /// Sets the HelpButtonElement property in the ViewModel
        /// </summary>
        private void HelpButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SoftwareAppsViewModel viewModel && sender is FrameworkElement button)
            {
                viewModel.HelpButtonElement = button;
            }
        }
    }
}
