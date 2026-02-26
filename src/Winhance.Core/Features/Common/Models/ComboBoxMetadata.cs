namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Typed metadata for ComboBox/Selection settings, replacing untyped CustomProperties dictionary entries.
/// </summary>
public record ComboBoxMetadata
{
    public required string[] DisplayNames { get; init; }
    public Dictionary<int, Dictionary<string, object?>>? ValueMappings { get; init; }
    public Dictionary<int, int>? SimpleValueMappings { get; init; }
    public Dictionary<int, bool>? CommandValueMappings { get; init; }
    public bool SupportsCustomState { get; init; }
    public string? CustomStateDisplayName { get; init; }
    public string[]? OptionTooltips { get; init; }
    public Dictionary<int, string>? OptionWarnings { get; init; }
    public Dictionary<int, (string Title, string Message)>? OptionConfirmations { get; init; }
}
