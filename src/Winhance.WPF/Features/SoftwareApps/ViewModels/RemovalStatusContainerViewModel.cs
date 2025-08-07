using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.SoftwareApps.Services;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// Container ViewModel for managing all removal status items
    /// </summary>
    public class RemovalStatusContainerViewModel : INotifyPropertyChanged, IDisposable
    {
        public RemovalStatusContainerViewModel(
            IScriptPathDetectionService scriptPathDetectionService,
            IScheduledTaskService scheduledTaskService,
            ILogService logService)
        {
            RemovalStatusItems = new ObservableCollection<RemovalStatusViewModel>
            {
                new RemovalStatusViewModel(
                    "Bloat Removal",
                    "DeleteSweep", // Generic cleanup icon
                    "#00FF3C", // Green
                    "BloatRemoval.ps1",
                    "BloatRemoval", // Try just the task name without folder
                    scriptPathDetectionService,
                    scheduledTaskService,
                    logService),

                new RemovalStatusViewModel(
                    "Microsoft Edge",
                    "MicrosoftEdge",
                    "#0078D4", // Microsoft Blue (we'll handle gradient in XAML)
                    "EdgeRemoval.ps1",
                    "Winhance\\EdgeRemoval",
                    scriptPathDetectionService,
                    scheduledTaskService,
                    logService),

                new RemovalStatusViewModel(
                    "OneDrive",
                    "MicrosoftOnedrive", // OneDrive icon
                    "#0078D4", // Microsoft blue
                    "OneDriveRemoval.ps1",
                    "Winhance\\OneDriveRemoval",
                    scriptPathDetectionService,
                    scheduledTaskService,
                    logService),

                new RemovalStatusViewModel(
                    "OneNote",
                    "MicrosoftOnenote", // OneNote icon
                    "#7719AA", // OneNote Purple
                    "OneNoteRemoval.ps1",
                    "Winhance\\OneNoteRemoval",
                    scriptPathDetectionService,
                    scheduledTaskService,
                    logService)
            };
        }

        public ObservableCollection<RemovalStatusViewModel> RemovalStatusItems { get; }

        /// <summary>
        /// Refreshes the status of all removal items
        /// </summary>
        public async Task RefreshAllStatusesAsync()
        {
            var tasks = RemovalStatusItems.Select(item => item.RefreshStatusAsync());
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
