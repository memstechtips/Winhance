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
    IReadOnlyList<Setting> GetByFeature(string featureId);
    Setting? GetById(string settingId);
    /// <summary>The owning feature id (e.g. "power", "update") for the given setting id, alias-normalized
    /// like GetById, or null if the setting is not present on this machine. The catalog-native replacement for
    /// the old registry's GetFeatureIdForSetting; the OS-portable membership resolves a "-win10" id on either
    /// OS, so the old bypassed fallback (GetFeatureIdForSettingBypassed) is obviated.</summary>
    string? GetFeatureIdForSetting(string settingId);
    IReadOnlyDictionary<string, IReadOnlyList<Setting>> GetAll();
}
