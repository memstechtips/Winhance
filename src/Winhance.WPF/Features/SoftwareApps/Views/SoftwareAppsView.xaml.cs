using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
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
            Loaded += SoftwareAppsView_Loaded;

            // Set up the expandable sections
            WindowsAppsContent.Visibility = Visibility.Collapsed;
            ExternalAppsContent.Visibility = Visibility.Collapsed;

            // Add click handlers
            WindowsAppsHeaderBorder.MouseDown += WindowsAppsHeader_MouseDown;
            ExternalAppsHeaderBorder.MouseDown += ExternalAppsHeader_MouseDown;
        }

        private async void SoftwareAppsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SoftwareAppsViewModel viewModel)
            {
                try
                {
                    // Initialize the view model
                    await viewModel.InitializeCommand.ExecuteAsync(null);
                    
                    // Expand the first section by default after initialization is complete
                    WindowsAppsContent.Visibility = Visibility.Visible;
                    WindowsAppsHeaderIcon.Kind = PackIconKind.ChevronUp; // Up arrow
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Error initializing Software Apps view: {ex.Message}",
                        "Initialization Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void WindowsAppsHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToggleSection(WindowsAppsContent, WindowsAppsHeaderIcon);
        }

        private void ExternalAppsHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToggleSection(ExternalAppsContent, ExternalAppsHeaderIcon);
        }

        private void ToggleSection(UIElement content, PackIcon icon)
        {
            if (content.Visibility == Visibility.Collapsed)
            {
                content.Visibility = Visibility.Visible;
                icon.Kind = PackIconKind.ChevronUp; // Up arrow (section expanded)
            }
            else
            {
                content.Visibility = Visibility.Collapsed;
                icon.Kind = PackIconKind.ChevronDown; // Down arrow (section collapsed)
            }
        }
    }
}
