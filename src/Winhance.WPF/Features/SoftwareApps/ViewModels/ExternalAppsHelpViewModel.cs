using System.Windows.Input;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// Simple ViewModel for ExternalAppsHelpContent to provide CloseHelpCommand
    /// </summary>
    public class ExternalAppsHelpViewModel
    {
        /// <summary>
        /// Command to close the help flyout
        /// </summary>
        public ICommand CloseHelpCommand { get; set; }
    }
}
