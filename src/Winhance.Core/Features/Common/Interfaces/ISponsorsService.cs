using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Live data from the sponsors branch; when that fails, the last good fetch saved on disk, then the bundled snapshot.
public interface ISponsorsService
{
    // Cached once a list loads and refetched behind the call when it gets old (24 h for a live list, 1 h for a
    // fallback); null when the live fetch and both fallbacks fail.
    Task<SponsorsDocument?> GetSponsorsAsync(CancellationToken cancellationToken = default);

    // The last good fetch or the bundled snapshot, no network: for surfaces that cannot wait, like the splash.
    Task<SponsorsDocument?> GetSnapshotAsync();

    // Saves the logos of a list fetched live this run, so the splash can show them without the network.
    Task CacheLogosAsync(CancellationToken cancellationToken = default);

    // Image URIs to try in order: the cached logo, the one bundled with the release, then the live file.
    IReadOnlyList<string> GetLogoSources(SponsorEntry sponsor);
}
