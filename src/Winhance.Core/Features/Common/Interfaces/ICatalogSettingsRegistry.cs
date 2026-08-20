using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

// Call InitializeAsync once before use; it probes the machine and resolves powercfg existence.
public interface ICatalogSettingsRegistry
{
    Task InitializeAsync();
    IReadOnlyList<Setting> GetByFeature(string featureId, CatalogScope scope = default);
    Setting? GetById(string settingId, CatalogScope scope = default);
    string? GetFeatureIdForSetting(string settingId);
    IReadOnlyDictionary<string, IReadOnlyList<Setting>> GetAll(CatalogScope scope = default);
}
