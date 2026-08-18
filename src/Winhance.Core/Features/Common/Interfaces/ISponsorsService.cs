using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Live data from the sponsors branch, with the bundled snapshot as fallback.
public interface ISponsorsService
{
    // Cached after the first fetch; null when both the live fetch and the bundled fallback fail.
    Task<SponsorsDocument?> GetSponsorsAsync(CancellationToken cancellationToken = default);

    string GetLogoUri(SponsorEntry sponsor);

    string? GetBundledLogoPath(SponsorEntry sponsor);
}
