using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal interface IStoreCatalogService
{
    // Keyed by package family name. Only apps with a newer version in the catalog appear; an app the catalog does not
    // know, or any failure, leaves no entry.
    Task<IReadOnlyDictionary<string, StoreCatalogUpdate>> GetUpdatesAsync(IReadOnlyList<StoreCatalogApp> apps, CancellationToken cancellationToken = default);
}
