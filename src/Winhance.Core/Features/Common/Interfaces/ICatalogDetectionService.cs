using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ICatalogDetectionService
{
    Task<Dictionary<string, CatalogDetectionResult>> DetectAsync(IReadOnlyCollection<Setting> settings);
}
