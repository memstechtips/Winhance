namespace Winhance.Core.Features.WimUtil.Models;

// The update list the refresh job publishes on the repo's data-only `updates` branch: per build and
// architecture, the latest cumulative update and the Microsoft Update Catalog files that install it.
public sealed record WindowsUpdateList(int Schema, DateTimeOffset GeneratedAt, IReadOnlyList<WindowsUpdateBuild> Builds);

public sealed record WindowsUpdateBuild(int Build, string Arch, string Name, IReadOnlyList<WindowsUpdate> Updates);

// Files run checkpoints first and the target last, the order DISM needs them in one folder.
public sealed record WindowsUpdate(
    string Kb,
    string Title,
    int TargetUbr,
    DateOnly ReleaseDate,
    string CatalogId,
    IReadOnlyList<WindowsUpdateFile> Files)
{
    public long DownloadBytes => Files.Sum(f => f.Size);
}

public sealed record WindowsUpdateFile(string Role, string Kb, string FileName, string Url, long Size, string Sha1, string Sha256);
