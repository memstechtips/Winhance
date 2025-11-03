using System.Windows;
using System.Windows.Controls;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    public partial class ExternalAppsTableView : UserControl
    {
        private bool _isLoaded;

        public ExternalAppsTableView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            if (DataContext is ExternalAppsViewModel vm && IsVisible)
            {
                vm.UpdateAllItemsCollection();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            if (DataContext is ExternalAppsViewModel vm)
            {
                vm.CleanupTableView();
            }
        }
    }
}
