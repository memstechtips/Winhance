using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.SoftwareApps.Services;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// ViewModel for the WindowsAppsHelpContent view
    /// </summary>
    public class WindowsAppsHelpContentViewModel : INotifyPropertyChanged
    {
        public WindowsAppsHelpContentViewModel(
            IScriptPathService scriptPathService,
            IScheduledTaskService scheduledTaskService,
            ILogService logService)
        {
            RemovalStatusContainer = new RemovalStatusContainerViewModel(
                scriptPathService,
                scheduledTaskService,
                logService);
            
            // Status checks start automatically when ViewModels are created
        }

        public RemovalStatusContainerViewModel RemovalStatusContainer { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
