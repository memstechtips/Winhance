using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

// Call InitializeAsync once before use; it probes the machine and resolves powercfg existence.
public interface ICatalogSettingsRegistry
{
    Task InitializeAsync();
    // includeOtherOsVersions relaxes ONLY the OS-build gate; hardware and existence still apply.
    IReadOnlyList<Setting> GetByFeature(string featureId, bool includeOtherOsVersions = false);
    Setting? GetById(string settingId, bool includeOtherOsVersions = false);
    string? GetFeatureIdForSetting(string settingId);
    IReadOnlyDictionary<string, IReadOnlyList<Setting>> GetAll(bool includeOtherOsVersions = false);
}
