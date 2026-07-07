using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>The catalog-sourced settings membership: which Settings exist per feature on THIS machine, filtered
/// exactly as the old CompatibleSettingsRegistry (OS build + hardware caps + powercfg existence), composed from the
/// proven pieces (SettingCatalog.ByFeature + CatalogMembershipFilter + CatalogPowerExistenceFilter). The additive
/// root of the SettingDefinition retirement: consumers repoint onto this, then the old registry sourcing is deleted.
/// Call InitializeAsync once before use (it probes the machine + resolves existence).</summary>
public interface ICatalogSettingsRegistry
{
    Task InitializeAsync();
    /// <summary>The settings of the given feature present on this machine. Default scope is current-OS
    /// (OS-build + hardware + existence); pass includeOtherOsVersions:true for the "show settings for other
    /// Windows versions" scope, which relaxes ONLY the OS-build gate (hardware + existence still apply).</summary>
    IReadOnlyList<Setting> GetByFeature(string featureId, bool includeOtherOsVersions = false);
    Setting? GetById(string settingId, bool includeOtherOsVersions = false);
    /// <summary>The owning feature id (e.g. "power", "update") for the given setting id, alias-normalized
    /// like GetById, or null if the id is not in the catalog (owning feature is OS-scope-independent). The catalog-native replacement for
    /// the old registry's GetFeatureIdForSetting; the OS-portable membership resolves a "-win10" id on either
    /// OS, so the old bypassed fallback (GetFeatureIdForSettingBypassed) is obviated.</summary>
    string? GetFeatureIdForSetting(string settingId);
    IReadOnlyDictionary<string, IReadOnlyList<Setting>> GetAll(bool includeOtherOsVersions = false);
}
