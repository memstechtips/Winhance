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
}

/// <summary>Drives the new catalog detection engine over a batch of settings against the live machine, returning
/// each setting's detected state keyed by Setting.Id. Builds one pre-fetched detection context per batch.</summary>
public interface ICatalogDetectionService
{
    Task<Dictionary<string, CatalogDetectionResult>> DetectAsync(IReadOnlyCollection<Setting> settings);
}
