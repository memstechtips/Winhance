using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.SoftwareApps.Services;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// ViewModel for the WindowsAppsHelpContent view
    /// </summary>
    public class WindowsAppsHelpContentViewModel : INotifyPropertyChanged, IDisposable
    {
        public WindowsAppsHelpContentViewModel(
            IScriptPathDetectionService scriptPathDetectionService,
            IScheduledTaskService scheduledTaskService,
            ILogService logService)
        {
            RemovalStatusContainer = new RemovalStatusContainerViewModel(
                scriptPathDetectionService,
                scheduledTaskService,
                logService);
            
            // Start status checks when Help dialog is shown
            _ = Task.Run(async () => await RemovalStatusContainer.RefreshAllStatusesAsync());
        }

        public RemovalStatusContainerViewModel RemovalStatusContainer { get; }

        /// <summary>
        /// Command to close the help flyout
        /// </summary>
        public ICommand CloseHelpCommand { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RemovalStatusContainer?.Dispose();
            }
        }
    }
}
