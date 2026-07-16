using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// The full-state provider: produces a complete typed <see cref="SettingStateResult"/> per catalog
/// <see cref="Setting"/> from the catalog detection engine. It is the live source of setting state, building the
/// whole result from the engine's <c>CatalogDetectionResult</c>. Its machine-independent behaviour (the
/// Windows-grounded IsEnabled rule and the selection value-match fallback) is gated by
/// <c>CatalogSettingStateProviderConformanceTests</c>.
/// </summary>
public interface ICatalogSettingStateProvider
{
    /// <summary>The detected state per catalog <see cref="Setting"/> keyed by <c>Setting.Id</c>, built from the
    /// detection engine.</summary>
    Task<Dictionary<string, SettingStateResult>> GetStatesAsync(IReadOnlyList<Setting> settings);
}
