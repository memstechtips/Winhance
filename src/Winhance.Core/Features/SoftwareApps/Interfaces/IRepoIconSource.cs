namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IRepoIconSource
{
    // Validated against expectedSha256 when provided; null on any failure, never throws.
    System.Threading.Tasks.Task<byte[]?> GetIconBytesAsync(
        string repoPath, string? expectedSha256, System.Threading.CancellationToken ct = default);
}
