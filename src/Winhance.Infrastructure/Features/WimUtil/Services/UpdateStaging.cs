using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// $OEM$\$1 lands on C: during Setup. DISM finds the target update's checkpoints in the folder the target sits in, so
// the folder holds that update's .msu files and nothing else, and the target's name is recorded in a file beside it.
// An update counts as staged only when that record names an .msu that is there.
internal static class UpdateStaging
{
    internal const string TargetRecordSuffix = "-target.txt";

    internal const string InstalledTargetRecord = ScriptPaths.UnattendUpdatesDirectory + TargetRecordSuffix;

    internal static string FolderOf(IFileSystemService files, string workingDirectory) =>
        files.CombinePath(workingDirectory, ScriptPaths.UnattendUpdatesMediaDirectory);

    internal static string TargetRecordOf(IFileSystemService files, string workingDirectory) =>
        FolderOf(files, workingDirectory) + TargetRecordSuffix;

    // The list's target entry, not the highest KB: the two agree only while KB numbers keep rising.
    internal static string? TargetOf(IFileSystemService files, string workingDirectory)
    {
        var record = TargetRecordOf(files, workingDirectory);
        if (!files.FileExists(record))
        {
            return null;
        }

        var name = files.ReadAllText(record).Trim();
        var target = files.CombinePath(FolderOf(files, workingDirectory), name);
        return name.Length > 0 && name.EndsWith(".msu", StringComparison.OrdinalIgnoreCase) && files.FileExists(target) ? target : null;
    }

    internal static string[] PackagesOf(IFileSystemService files, string workingDirectory)
    {
        var folder = FolderOf(files, workingDirectory);
        return files.DirectoryExists(folder) ? files.GetFiles(folder, "*.msu") : [];
    }

    internal static bool IsStaged(IFileSystemService files, string workingDirectory) => TargetOf(files, workingDirectory) is not null;
}
