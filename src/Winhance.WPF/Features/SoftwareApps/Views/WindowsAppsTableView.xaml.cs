using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    /// <summary>
    /// Interaction logic for WindowsAppsTableView.xaml
    /// </summary>
    public partial class WindowsAppsTableView : UserControl
    {
        public WindowsAppsTableView()
        {
            InitializeComponent();
        }

        private void DataGridColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is DataGridColumnHeader header && DataContext is WindowsAppsViewModel viewModel)
            {
                string? sortProperty = null;
                
                switch (header.Content?.ToString())
                {
                    case "Name":
                        sortProperty = "Name";
                        break;
                    case "Type":
                        sortProperty = "ItemType";
                        break;
                    case "Status":
                        sortProperty = "IsInstalled";
                        break;
                    case "Installable":
                        sortProperty = "CanBeReinstalled";
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
