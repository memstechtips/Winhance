using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// Where drivers wait on the media until Setup, or offline servicing, installs them. An empty folder
// counts as nothing staged: no step is written for it and no image is mounted for it.
internal static class DriverStaging
{
    internal static string FolderOf(IFileSystemService files, string workingDirectory) =>
        files.CombinePath(workingDirectory, "sources", "$OEM$", "$$", "Drivers");

    internal static bool IsStaged(IFileSystemService files, string workingDirectory)
    {
        var folder = FolderOf(files, workingDirectory);
        return HasFiles(files, folder);
    }

    // The media ROOT, not sources\: KB 2686316 publishes the paths Setup scans for storage drivers and every one is a
    // drive root (C:\$WinPEDriver$, D:\, E:\, X:\). Servicing never empties it, so Setup's first screen still finds them.
    internal static string WinpeFolderOf(IFileSystemService files, string workingDirectory) =>
        files.CombinePath(workingDirectory, "$WinpeDriver$");

    internal static bool HasAnyOnMedia(IFileSystemService files, string workingDirectory) =>
        IsStaged(files, workingDirectory) || HasFiles(files, WinpeFolderOf(files, workingDirectory));

    private static bool HasFiles(IFileSystemService files, string folder) =>
        files.DirectoryExists(folder) && files.GetFiles(folder, "*", SearchOption.AllDirectories).Length > 0;
}
