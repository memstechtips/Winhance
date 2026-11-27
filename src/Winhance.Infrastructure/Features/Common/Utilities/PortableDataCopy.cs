using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Utilities;

// A portable copy's first run takes the installed copy's settings, backups and change history, once. The
// originals stay, because an installed Winhance on this PC still reads them. Logs and caches rebuild themselves.
internal sealed class PortableDataCopy(IFileSystemService fileSystem) : IPortableDataCopy
{
    public IReadOnlyList<string> CopyInstalledData(string localAppData) =>
        AppDataPaths.IsPortable
            ? CopyInstalledData(AppDataPaths.MachineRoot, AppDataPaths.InstalledUserRoot(localAppData), AppDataPaths.InstalledMachineRoot)
            : [];

    internal IReadOnlyList<string> CopyInstalledData(string portableRoot, string installedUserRoot, string installedMachineRoot)
    {
        var copied = new List<string>();
        foreach (var folder in new[] { "Config", "Backup" })
        {
            var source = Path.Combine(installedUserRoot, folder);
            var target = Path.Combine(portableRoot, folder);
            if (!fileSystem.DirectoryExists(source) || fileSystem.DirectoryExists(target))
                continue;

            // Into a temp folder first, so a copy cut short is retried next run instead of counting as done.
            var staging = target + ".copying";
            if (fileSystem.DirectoryExists(staging))
                fileSystem.DeleteDirectory(staging, recursive: true);
            foreach (var file in fileSystem.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(staging, Path.GetRelativePath(source, file));
                fileSystem.CreateDirectory(Path.GetDirectoryName(destination)!);
                fileSystem.CopyFile(file, destination);
            }
            fileSystem.CreateDirectory(staging);
            fileSystem.MoveDirectory(staging, target);
            copied.Add(target);
        }

        var history = Path.Combine(installedMachineRoot, "ChangeHistory.txt");
        var historyTarget = Path.Combine(portableRoot, "ChangeHistory.txt");
        if (fileSystem.FileExists(history) && !fileSystem.FileExists(historyTarget))
        {
            fileSystem.CreateDirectory(portableRoot);
            fileSystem.CopyFile(history, historyTarget);
            copied.Add(historyTarget);
        }

        return copied;
    }
}
