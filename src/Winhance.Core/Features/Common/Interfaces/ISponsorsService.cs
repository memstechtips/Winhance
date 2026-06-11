using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Fetches and caches sponsor/supporter data from the sponsors branch, with bundled fallback.
/// </summary>
public interface ISponsorsService
{
    /// <summary>
    /// Returns the sponsors document for the current session (cached after first fetch).
    /// Returns null if both the live fetch and the bundled fallback fail.
    /// </summary>
    Task<SponsorsDocument?> GetSponsorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the remote raw URL for a sponsor's logo asset on the sponsors branch.
    /// </summary>
    string GetLogoUri(SponsorEntry sponsor);

    /// <summary>
    /// Returns an ms-appx:/// URI for the bundled logo snapshot if the file exists on disk,
    /// otherwise null.
    /// </summary>
    string? GetBundledLogoPath(SponsorEntry sponsor);
}
