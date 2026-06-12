namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IRepoIconSource
{
    /// <summary>Returns the raw image bytes for a repo path (e.g. "icons/windows/x.png"),
    /// validated against expectedSha256 when provided, or null on any failure. Never throws.</summary>
    System.Threading.Tasks.Task<byte[]?> GetIconBytesAsync(
        string repoPath, string? expectedSha256, System.Threading.CancellationToken ct = default);
}
