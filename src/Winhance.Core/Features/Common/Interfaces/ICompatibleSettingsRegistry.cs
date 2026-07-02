using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ICompatibleSettingsRegistry
{
    Task InitializeAsync();
    IEnumerable<SettingDefinition> GetFilteredSettings(string featureId);
    IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllFilteredSettings();
    IEnumerable<SettingDefinition> GetBypassedSettings(string featureId);
    IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllBypassedSettings();
    void SetFilterEnabled(bool enabled);
    bool IsInitialized { get; }

    /// <summary>
    /// Returns the SettingDefinition for the given id, or null if not registered.
    /// Respects the current filter mode (filtered vs bypassed).
    /// </summary>
    SettingDefinition? GetById(string settingId);

    /// <summary>
    /// Returns the feature id (e.g. "update", "power") that owns the given setting,
    /// or null if not registered. Used by SettingLocalizationService and
    /// RecommendedSettingsApplier for cross-cutting lookups.
    /// </summary>
    string? GetFeatureIdForSetting(string settingId);
}
