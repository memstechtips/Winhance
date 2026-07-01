using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// The full-state provider: produces a complete typed <see cref="SettingStateResult"/> per setting from the catalog
/// detection engine ALONE. It is the live source of setting state after old discovery was retired (Phase 6.9); it
/// replaced the old-discovery + <c>CatalogDetectionStateOverlay</c> hybrid, building the whole result from the new
/// engine's <c>CatalogDetectionResult</c>.
///
/// Pairs a def to its catalog Setting by normalized Id (via <c>SettingIdAliases</c>, so the retired OS-merged
/// "-win10" variants resolve to their canonical merged Setting); a def with no catalog peer even after normalizing is
/// returned as an unsuccessful result rather than throwing. Its machine-independent behaviour (the Windows-grounded
/// IsEnabled rule, alias pairing, and the selection value-match fallback) is gated by
/// <c>CatalogSettingStateProviderConformanceTests</c>.
/// </summary>
public interface ICatalogSettingStateProvider
{
    /// <summary>The detected state per setting keyed by <c>SettingDefinition.Id</c>, built from the new engine
    /// alone.</summary>
    Task<Dictionary<string, SettingStateResult>> GetStatesAsync(IReadOnlyList<SettingDefinition> settings);
}
