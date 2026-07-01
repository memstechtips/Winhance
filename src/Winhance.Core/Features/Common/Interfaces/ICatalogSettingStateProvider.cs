using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Phase 6.8 full-state provider (additive, transitional): produces a complete typed
/// <see cref="SettingStateResult"/> per setting from the NEW catalog detection engine ALONE, with NO call to the old
/// <see cref="ISystemSettingsDiscoveryService.GetSettingStatesAsync"/>. This is the eventual replacement for the
/// old-discovery + <c>CatalogDetectionStateOverlay</c> hybrid: where the overlay layers the new engine onto an
/// old-discovery base, this builds the whole result from the new engine's <c>CatalogDetectionResult</c>.
///
/// Pairs a def to its catalog Setting by normalized Id (via <c>SettingIdAliases</c>, so the retired OS-merged
/// "-win10" variants resolve to their canonical merged Setting); a def with no catalog peer even after normalizing is
/// returned as an unsuccessful result rather than throwing. Wired to no consumer yet - its correctness is gated by
/// <c>FullStateProviderEquivalenceTests</c>, which proves it matches the live hybrid for paired settings.
/// </summary>
public interface ICatalogSettingStateProvider
{
    /// <summary>Drop-in shape for <see cref="ISystemSettingsDiscoveryService.GetSettingStatesAsync"/>: the detected
    /// state per setting keyed by <c>SettingDefinition.Id</c>, built from the new engine alone.</summary>
    Task<Dictionary<string, SettingStateResult>> GetStatesAsync(IReadOnlyList<SettingDefinition> settings);
}
