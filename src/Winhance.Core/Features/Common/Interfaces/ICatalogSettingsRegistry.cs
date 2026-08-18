using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>The catalog-sourced settings membership: which Settings exist per feature on THIS machine, filtered
/// by OS build + hardware caps + powercfg existence, composed from SettingCatalog.ByFeature +
/// CatalogMembershipFilter + CatalogPowerExistenceFilter. Call InitializeAsync once before use (it probes the
/// machine + resolves existence).</summary>
public interface ICatalogSettingsRegistry
{
    Task InitializeAsync();
    /// <summary>The settings of the given feature present on this machine. Default scope is current-OS
    /// (OS-build + hardware + existence); pass includeOtherOsVersions:true for the "show settings for other
    /// Windows versions" scope, which relaxes ONLY the OS-build gate (hardware + existence still apply).</summary>
    IReadOnlyList<Setting> GetByFeature(string featureId, bool includeOtherOsVersions = false);
    Setting? GetById(string settingId, bool includeOtherOsVersions = false);
    /// <summary>The owning feature id (e.g. "power", "update") for the given setting id, alias-normalized
    /// like GetById, or null if the id is not in the catalog (owning feature is OS-scope-independent).</summary>
    string? GetFeatureIdForSetting(string settingId);
    IReadOnlyDictionary<string, IReadOnlyList<Setting>> GetAll(bool includeOtherOsVersions = false);
}
