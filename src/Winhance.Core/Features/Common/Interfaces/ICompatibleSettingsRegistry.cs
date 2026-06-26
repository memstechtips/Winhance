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

    /// <summary>
    /// Returns the SettingDefinition for the given id from the Windows-version-BYPASSED index, ignoring the current
    /// filter mode, or null if not registered at all. Used by the config-apply path to resolve a merged catalog
    /// setting whose OLD def is OS-filtered-out on this machine (e.g. a This PC folder setting imported from a
    /// "-win10" config and normalized to its canonical id, running on the other OS) so the NEW engine can apply it
    /// OS-portably. Callers must gate this on the setting being a build-compatible catalog peer.
    /// </summary>
    SettingDefinition? GetByIdBypassed(string settingId);

    /// <summary>
    /// Returns the owning feature id for the given setting from the BYPASSED index, ignoring the current filter mode,
    /// or null if not registered. The bypassed companion to <see cref="GetFeatureIdForSetting"/> for the same
    /// cross-OS config-apply resolution.
    /// </summary>
    string? GetFeatureIdForSettingBypassed(string settingId);
}
