using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Catalog;

// Outcome says WHY a null label is null - unrecognized content, a wrong stored type, or a detection failure; the UI
// must distinguish them because only the first two are safe to act on.
public sealed record CatalogDetectionResult
{
    public string? StateLabel { get; init; }
    public int? Value { get; init; }
    public bool Detected { get; init; }

    // Defaults to Resolved so an unset value never invents a problem. The service's catch-all reports Undetermined:
    // detection failing is OUR failure and must not masquerade as an unrecognized value on the user's machine.
    public SettingDetectionOutcome Outcome { get; init; } = SettingDetectionOutcome.Resolved;

    // For the log and issue reports; never rendered raw in the UI.
    public string? OutcomeDetail { get; init; }

    // Null for non-powercfg settings. Interpretation stays UI-side (selection index via ValueMappings, or display units).
    public int? AcValue { get; init; }
    public int? DcValue { get; init; }

    // StateLabel carries the current selection's Value (e.g. the scheme GUID) so the choice resolves by value, no index round-trip.
    public IReadOnlyList<DynamicOption>? Options { get; init; }

    public string? DynamicSelectionName { get; init; }

    // Keyed by ValueName ?? "KeyExists"; the source the config-export custom-state path reads.
    public IReadOnlyDictionary<string, object?>? Readings { get; init; }

    // The active adapter's IPv4 servers, in adapter order; null for every setting without a DnsServerDetector.
    // A Custom reading has to carry them so another machine can be put on the same servers.
    public IReadOnlyList<string>? DnsServers { get; init; }
}

public interface ICatalogDetectionService
{
    Task<Dictionary<string, CatalogDetectionResult>> DetectAsync(IReadOnlyCollection<Setting> settings);
}
