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
}

/// <summary>Drives the new catalog detection engine over a batch of settings against the live machine, returning
/// each setting's detected state keyed by Setting.Id. Builds one pre-fetched detection context per batch.</summary>
public interface ICatalogDetectionService
{
    Task<Dictionary<string, CatalogDetectionResult>> DetectAsync(IReadOnlyCollection<Setting> settings);
}
