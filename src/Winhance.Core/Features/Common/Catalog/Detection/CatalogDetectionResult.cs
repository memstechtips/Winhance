using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The detected state for one setting. StateLabel is the resolved state label for a
/// state-based setting (toggle / selection / custom-detector) - null means it resolved to no state; Value is the
/// raw reading for a numeric (slider) setting; Detected is false when the engine resolved nothing.
/// <see cref="Outcome"/> says WHY a null label is null - unrecognized content, a wrong stored type, or a
/// detection failure - which the UI must distinguish because only the first two are safe to act on.</summary>
public sealed record CatalogDetectionResult
{
    public string? StateLabel { get; init; }
    public int? Value { get; init; }
    public bool Detected { get; init; }

    /// <summary>Why the setting did not resolve, when it did not. Defaults to
    /// <see cref="SettingDetectionOutcome.Resolved"/> so an unset value never invents a problem; the
    /// service sets it explicitly on every path, including its catch-all (which reports
    /// <see cref="SettingDetectionOutcome.Undetermined"/> - detection failing is OUR failure, and must not
    /// masquerade as an unrecognized value on the user's machine).</summary>
    public SettingDetectionOutcome Outcome { get; init; } = SettingDetectionOutcome.Resolved;

    /// <summary>Diagnostic detail for a non-resolved outcome (which value, expected vs actual registry kind,
    /// or the exception message). For the log and issue reports; never rendered raw in the UI.</summary>
    public string? OutcomeDetail { get; init; }

    /// <summary>Raw powercfg value indices for a setting with a live <see cref="PowerCfgTarget"/>, read per power
    /// context.
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
    /// plan's OS name). Null for a
    /// static-state setting or when no selection. The UI shows it as-is; <see cref="StateLabel"/> carries the GUID.</summary>
    public string? DynamicSelectionName { get; init; }

    /// <summary>The live per-registry-target readings for a setting's <see cref="RegTarget"/>s, keyed by
    /// <c>ValueName ?? "KeyExists"</c> - the same grouping, HKLM-first first-non-null mirror fold, REG_BINARY
    /// bit/byte reduction, and key-existence-as-bool. This is the source the config-export custom-state path
    /// reads (the unrecognized "-1"/Custom registry readings). Null for a setting with no registry targets.</summary>
    public IReadOnlyDictionary<string, object?>? Readings { get; init; }
}

/// <summary>Drives the catalog detection engine over a batch of settings against the live machine, returning
/// each setting's detected state keyed by Setting.Id. Builds one pre-fetched detection context per batch.</summary>
public interface ICatalogDetectionService
{
    Task<Dictionary<string, CatalogDetectionResult>> DetectAsync(IReadOnlyCollection<Setting> settings);
}
