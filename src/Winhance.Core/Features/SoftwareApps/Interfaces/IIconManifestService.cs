namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IIconManifestService
{
    // Cached in memory for the session. False on any failure (offline, parse error); never throws.
    System.Threading.Tasks.Task<bool> LoadAsync(System.Threading.CancellationToken ct = default);

    string? Sha256For(string repoPath);
}
