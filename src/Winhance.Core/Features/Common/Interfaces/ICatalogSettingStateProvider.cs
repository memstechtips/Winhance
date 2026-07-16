using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// The full-state provider: produces a complete typed <see cref="SettingStateResult"/> per catalog
/// <see cref="Setting"/> from the catalog detection engine ALONE. It is the live source of setting state after old
/// discovery was retired (Phase 6.9); it replaced the old-discovery + <c>CatalogDetectionStateOverlay</c> hybrid,
/// building the whole result from the new engine's <c>CatalogDetectionResult</c>.
///
/// Catalog-native since Slice 4bb-2; the def-based overload (which paired each def to its catalog Setting via
/// <c>SettingIdAliases</c>) was retired in Slice L6 once the last reader consumers moved onto catalog Settings.
/// Its machine-independent behaviour (the Windows-grounded IsEnabled rule and the selection value-match fallback)
/// is gated by <c>CatalogSettingStateProviderConformanceTests</c>.
/// </summary>
public interface ICatalogSettingStateProvider
{
    /// <summary>The detected state per catalog <see cref="Setting"/> keyed by <c>Setting.Id</c>, built from the
    /// new engine alone.</summary>
    Task<Dictionary<string, SettingStateResult>> GetStatesAsync(IReadOnlyList<Setting> settings);
}
