using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Typed metadata for ComboBox/Selection settings. Each option carries its own
/// DisplayName, ValueMappings, flags (IsDefault/IsRecommended), tooltip,
/// warning, confirmation, and script variables as a single typed record.
/// </summary>
public record ComboBoxMetadata
{
    public required IReadOnlyList<ComboBoxOption> Options { get; init; }
    public bool SupportsCustomState { get; init; }
    public string? CustomStateDisplayName { get; init; }
}
