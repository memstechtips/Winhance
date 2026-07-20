using System.Collections.Generic;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Models;

public sealed record SettingStateResult
{
    public bool IsEnabled { get; init; }
    public object? CurrentValue { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Raw AC/DC powercfg values for a separate-mode power setting, so the UI
    /// reads AC/DC from a typed field. Null for non-powercfg settings.</summary>
    public int? AcValue { get; init; }
    public int? DcValue { get; init; }

    /// <summary>For a setting whose options are produced at runtime (an <see cref="IDynamicOptionSource"/>, e.g. the
    /// power plan): the live options to show, and the current selection's <see cref="DynamicOption.Value"/> (the
    /// scheme GUID). Null for a normal static-state setting. Threaded by the detection overlay;
    /// the UI factory binds the dropdown to these (no index round-trip).</summary>
    public IReadOnlyList<DynamicOption>? DynamicOptions { get; init; }
    public string? DynamicSelection { get; init; }

    /// <summary>The active dynamic selection's RAW display NAME (the power plan's OS name). Threaded by the detection
    /// overlay; null for a non-dynamic setting.</summary>
    public string? DynamicSelectionName { get; init; }

    /// <summary>Live per-registry-target readings, keyed by <c>ValueName ?? "KeyExists"</c>, so the config-export
    /// custom-state path reads the unrecognized "-1"/Custom registry values from here. Threaded by the detection
    /// overlay from <see cref="Winhance.Core.Features.Common.Catalog.CatalogDetectionResult.Readings"/>; null for a
    /// setting with no registry targets or before the overlay runs.</summary>
    public IReadOnlyDictionary<string, object?>? Readings { get; init; }
}
