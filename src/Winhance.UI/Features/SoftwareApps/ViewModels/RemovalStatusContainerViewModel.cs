using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

public class RemovalStatusContainerViewModel(
    IScheduledTaskService scheduledTaskService,
    ILogService logService,
    IFileSystemService fileSystemService) : INotifyPropertyChanged, IDisposable
{
    // A catalog script with no entry here still gets a row, with the bloat look.
    private static readonly Dictionary<string, (string IconPath, string ActiveColor)> Looks = new()
    {
        [BloatRemovalScriptGenerator.ScriptName] = ("DeleteSweepIconPath", "#00FF3C"),
        ["EdgeRemoval"] = ("MicrosoftEdgeIconPath", "#0078D4"),
        ["OneDriveRemoval"] = ("MicrosoftOneDriveIconPath", "#0078D4"),
    };

    public ObservableCollection<RemovalStatusViewModel> RemovalStatusItems { get; } = new(
        WindowsAppDefinitions.GetWindowsApps().Items
            .Where(a => a.RemovalScriptName != null).DistinctBy(a => a.RemovalScriptName)
            .Select(a => (Label: a.Name, ScriptName: a.RemovalScriptName!))
            .Prepend(("Bloat Removal", BloatRemovalScriptGenerator.ScriptName))
            .Select(row =>
            {
                var (label, scriptName) = row;
                var (iconPath, activeColor) = Looks.GetValueOrDefault(scriptName, Looks[BloatRemovalScriptGenerator.ScriptName]);
                return new RemovalStatusViewModel(
                    label,
                    iconPath,
                    activeColor,
                    scriptName + ".ps1",
                    scriptName,
                    scheduledTaskService,
                    logService,
                    fileSystemService);
            }));

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
