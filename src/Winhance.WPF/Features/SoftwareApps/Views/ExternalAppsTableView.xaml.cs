using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    /// <summary>
    /// Interaction logic for ExternalAppsTableView.xaml
    /// </summary>
    public partial class ExternalAppsTableView : UserControl
    {
        public ExternalAppsTableView()
        {
            InitializeComponent();
        }

        private void DataGridColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is DataGridColumnHeader header && DataContext is ExternalAppsViewModel viewModel)
            {
                string? sortProperty = null;
                
                switch (header.Content?.ToString())
                {
                    case "Name":
                        sortProperty = "Name";
                        break;
                    case "Package ID":
                        sortProperty = "PackageName";
                        break;
                    case "Category":
                        sortProperty = "Category";
                        break;
                    case "Source":
                        sortProperty = "Source";
                        break;
                }

                if (!string.IsNullOrEmpty(sortProperty) && viewModel.SortByCommand?.CanExecute(sortProperty) == true)
                {
                    viewModel.SortByCommand.Execute(sortProperty);
                }
            }
        }
    }
}
