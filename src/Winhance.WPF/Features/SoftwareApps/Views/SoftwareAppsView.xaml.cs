using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    public partial class SoftwareAppsView : UserControl
    {
        public SoftwareAppsView()
        {
            InitializeComponent();
            DataContextChanged += SoftwareAppsView_DataContextChanged;
        }

        private void HelpButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SoftwareAppsViewModel viewModel && sender is FrameworkElement button)
            {
                viewModel.HelpButtonElement = button;
            }
        }

        private void HelpFlyoutOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is SoftwareAppsViewModel viewModel)
            {
                viewModel.HideHelpFlyoutCommand.Execute(null);
            }
        }

        private void HelpFlyoutOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DataContext is SoftwareAppsViewModel viewModel)
            {
                viewModel.HideHelpFlyoutCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SoftwareAppsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SoftwareAppsViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is SoftwareAppsViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareAppsViewModel.ShouldFocusHelpOverlay))
            {
                HelpFlyoutOverlay.Focus();
            }
        }
    }
}
