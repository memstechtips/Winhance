using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

public class RemovalStatusContainerViewModel(
    IScheduledTaskService scheduledTaskService,
    ILogService logService,
    IFileSystemService fileSystemService) : INotifyPropertyChanged, IDisposable
{
    public ObservableCollection<RemovalStatusViewModel> RemovalStatusItems { get; } = new()
    {
        new RemovalStatusViewModel(
            "Bloat Removal",
            "DeleteSweepIconPath",
            "#00FF3C",
            "BloatRemoval.ps1",
            "BloatRemoval",
            scheduledTaskService,
            logService,
            fileSystemService),

        new RemovalStatusViewModel(
            "Microsoft Edge",
            "MicrosoftEdgeIconPath",
            "#0078D4",
            "EdgeRemoval.ps1",
            "EdgeRemoval",
            scheduledTaskService,
            logService,
            fileSystemService),

        new RemovalStatusViewModel(
            "OneDrive",
            "MicrosoftOneDriveIconPath",
            "#0078D4",
            "OneDriveRemoval.ps1",
            "OneDriveRemoval",
            scheduledTaskService,
            logService,
            fileSystemService)
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
