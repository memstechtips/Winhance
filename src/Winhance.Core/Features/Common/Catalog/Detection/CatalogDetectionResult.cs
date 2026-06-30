using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The new engine's detected state for one setting. StateLabel is the resolved state label for a
/// state-based setting (toggle / selection / custom-detector) - null means Custom; Value is the raw reading for a
/// numeric (slider) setting; Detected is false when the engine resolved nothing.</summary>
public sealed record CatalogDetectionResult
{
    public string? StateLabel { get; init; }
    public int? Value { get; init; }
    public bool Detected { get; init; }

    /// <summary>Raw powercfg value indices for a setting with a live <see cref="PowerCfgTarget"/>, read per power
    /// context (the same raw units the old <c>RawValues["ACValue"]/["DCValue"]</c> carried - PowerReadAC/DCValueIndex).
    /// Null for non-powercfg settings. The UI maps these to a selection index (via the option ValueMappings) or a
    /// display number (via the setting's Units); the interpretation stays UI-side. <see cref="Value"/> already carries
    /// the AC reading for a numeric powercfg setting; these expose AC and DC distinctly for the AC/DC binding.</summary>
    public int? AcValue { get; init; }
    public int? DcValue { get; init; }

    /// <summary>The runtime-enumerated options for a setting whose options come from an
    /// <see cref="IDynamicOptionSource"/> (e.g. the installed power plans), in display order; null for a setting with
    /// static States. The UI binds these directly, and <see cref="StateLabel"/> carries the current selection's
    /// <see cref="DynamicOption.Value"/> (e.g. the active scheme GUID) so the chosen option resolves by value, with
    /// no index round-trip.</summary>
    public IReadOnlyList<DynamicOption>? Options { get; init; }

    /// <summary>For a dynamic-option setting (the power plan): the current selection's RAW display NAME (the active
    /// plan's OS name), read from the same source as the old discovery's RawValues["ActivePowerPlan"]. Null for a
    /// static-state setting or when no selection. The UI shows it as-is; <see cref="StateLabel"/> carries the GUID.</summary>
    public string? DynamicSelectionName { get; init; }

    /// <summary>The live per-registry-target readings for a setting's <see cref="RegTarget"/>s, keyed by
    /// <c>ValueName ?? "KeyExists"</c> exactly as the old discovery's <c>RawValues</c> were - the same grouping,
    /// HKLM-first first-non-null mirror fold, REG_BINARY bit/byte reduction, and key-existence-as-bool. This is the
    /// source the config-export custom-state path reads (the unrecognized "-1"/Custom registry readings), moved off
    /// the legacy detection RawValues onto the new engine. Null for a setting with no registry targets. Transitional -
    /// retired with the old RawValues at teardown.</summary>
    public IReadOnlyDictionary<string, object?>? Readings { get; init; }
}

/// <summary>Drives the new catalog detection engine over a batch of settings against the live machine, returning
/// each setting's detected state keyed by Setting.Id. Builds one pre-fetched detection context per batch.</summary>
public interface ICatalogDetectionService
{
    Task<Dictionary<string, CatalogDetectionResult>> DetectAsync(IReadOnlyCollection<Setting> settings);
}
