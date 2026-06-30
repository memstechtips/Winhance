using System.Collections.Generic;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Models;

public sealed record SettingStateResult
{
    public bool IsEnabled { get; init; }
    public object? CurrentValue { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, object?>? RawValues { get; init; }

    /// <summary>Raw AC/DC powercfg values for a separate-mode power setting (the new engine's reading), so the UI
    /// reads AC/DC from a typed field instead of RawValues["ACValue"/"DCValue"]. Null for non-powercfg settings.</summary>
    public int? AcValue { get; init; }
    public int? DcValue { get; init; }

    /// <summary>For a setting whose options are produced at runtime (an <see cref="IDynamicOptionSource"/>, e.g. the
    /// power plan): the live options to show, and the current selection's <see cref="DynamicOption.Value"/> (the
    /// scheme GUID). Null for a normal static-state setting. Threaded by the detection overlay from the new engine;
    /// the UI factory binds the dropdown to these (no index round-trip). Transitional - retired with this type.</summary>
    public IReadOnlyList<DynamicOption>? DynamicOptions { get; init; }
    public string? DynamicSelection { get; init; }

    /// <summary>The new engine's live per-registry-target readings, keyed by <c>ValueName ?? "KeyExists"</c> exactly
    /// as the legacy detection RawValues were, so the config-export custom-state path reads the unrecognized
    /// "-1"/Custom registry values from the new engine instead of RawValues. Threaded by the detection overlay from
    /// <see cref="Winhance.Core.Features.Common.Catalog.CatalogDetectionResult.Readings"/>; null for a setting with no
    /// registry targets or before the overlay runs. Transitional - retired with this type.</summary>
    public IReadOnlyDictionary<string, object?>? Readings { get; init; }
}
