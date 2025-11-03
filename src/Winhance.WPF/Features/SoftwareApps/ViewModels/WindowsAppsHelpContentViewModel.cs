using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public class WindowsAppsHelpContentViewModel(
        IScheduledTaskService scheduledTaskService,
        ILogService logService) : INotifyPropertyChanged, IDisposable
    {
        public RemovalStatusContainerViewModel RemovalStatusContainer { get; } = new(
            scheduledTaskService,
            logService);

        public ICommand CloseHelpCommand { get; set; } = null!;

        public void Initialize()
        {
            _ = Task.Run(async () => await RemovalStatusContainer.RefreshAllStatusesAsync());
        }

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
