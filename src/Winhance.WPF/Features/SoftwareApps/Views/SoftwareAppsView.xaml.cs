using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            
            // Subscribe to DataContext changes to handle focus triggering
            DataContextChanged += SoftwareAppsView_DataContextChanged;
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

        /// <summary>
        /// Handles clicking outside the help flyout to close it
        /// This is pure UI behavior and can remain in the View (following MoreMenuFlyout pattern)
        /// </summary>
        private void HelpFlyoutOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Close the flyout when clicking on the overlay (outside the help content)
            if (DataContext is SoftwareAppsViewModel viewModel)
            {
                viewModel.HideHelpFlyoutCommand.Execute(null);
            }
        }

        /// <summary>
        /// Handles keyboard input on the help flyout overlay (Escape to close)
        /// This is pure UI behavior and can remain in the View (following MoreMenuFlyout pattern)
        /// </summary>
        private void HelpFlyoutOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            // Close the flyout when pressing Escape
            if (e.Key == Key.Escape && DataContext is SoftwareAppsViewModel viewModel)
            {
                viewModel.HideHelpFlyoutCommand.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles DataContext changes to subscribe to ViewModel property changes
        /// </summary>
        private void SoftwareAppsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel
            if (e.OldValue is SoftwareAppsViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            
            // Subscribe to new ViewModel
            if (e.NewValue is SoftwareAppsViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        /// <summary>
        /// Handles ViewModel property changes to trigger focus when needed
        /// </summary>
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareAppsViewModel.ShouldFocusHelpOverlay))
            {
                // Focus the overlay to enable keyboard input
                HelpFlyoutOverlay.Focus();
            }
        }
    }
}
