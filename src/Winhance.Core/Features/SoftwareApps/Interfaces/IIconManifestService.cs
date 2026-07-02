namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IIconManifestService
{
    /// <summary>Fetch manifest.json from the repo (@main), parse it, cache in memory for
    /// this session. Returns false on any failure (offline, parse error). Never throws.</summary>
    System.Threading.Tasks.Task<bool> LoadAsync(System.Threading.CancellationToken ct = default);

    /// <summary>The expected sha256 for a repo path (e.g. "icons/windows/x.png"), or null if
    /// the path isn't in the manifest or the manifest hasn't loaded.</summary>
    string? Sha256For(string repoPath);
}
