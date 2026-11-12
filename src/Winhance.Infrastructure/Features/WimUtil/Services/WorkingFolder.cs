using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal static class WorkingFolder
{
    // Files copied off the media or into the staging folder keep a read-only bit, and a recursive delete refuses a
    // read-only file. A link is left alone: resetting its attributes would reach whatever it points at.
    internal static void Delete(IFileSystemService files, string folder)
    {
        foreach (var file in files.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (!files.GetFileAttributes(file).HasFlag(FileAttributes.ReparsePoint))
            {
                files.SetFileAttributes(file, FileAttributes.Normal);
            }
        }
        files.DeleteDirectory(folder, true);
    }
}
