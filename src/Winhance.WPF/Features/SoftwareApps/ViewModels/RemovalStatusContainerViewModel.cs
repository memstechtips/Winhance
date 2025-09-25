using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public class RemovalStatusContainerViewModel(
        IScheduledTaskService scheduledTaskService,
        ILogService logService) : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<RemovalStatusViewModel> RemovalStatusItems { get; } = new()
        {
            new RemovalStatusViewModel(
                "Bloat Removal",
                "DeleteSweep",
                "#00FF3C",
                "BloatRemoval.ps1",
                "BloatRemoval",
                scheduledTaskService,
                logService),

            new RemovalStatusViewModel(
                "Microsoft Edge",
                "MicrosoftEdge",
                "#0078D4",
                "EdgeRemoval.ps1",
                "EdgeRemoval",
                scheduledTaskService,
                logService),

            new RemovalStatusViewModel(
                "OneDrive",
                "MicrosoftOnedrive",
                "#0078D4",
                "OneDriveRemoval.ps1",
                "OneDriveRemoval",
                scheduledTaskService,
                logService)
        };

        public async Task RefreshAllStatusesAsync()
        {
            var tasks = RemovalStatusItems.Select(item => item.StartStatusMonitoringAsync());
            await Task.WhenAll(tasks);
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
                foreach (var item in RemovalStatusItems)
                {
                    item.Dispose();
                }
            }
        }
    }
}
