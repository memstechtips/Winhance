using System.Windows.Controls;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{

    public partial class WindowsAppsHelpContent : UserControl
    {
        public WindowsAppsHelpContent()
        {
            InitializeComponent();
        }

        public WindowsAppsHelpContent(WindowsAppsHelpContentViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
