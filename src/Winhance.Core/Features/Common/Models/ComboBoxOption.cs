using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// One ComboBox option. Replaces the nine parallel dictionaries on ComboBoxMetadata with a single
/// typed record per option - authors no longer need to keep index-keyed dictionaries in sync by hand.
/// </summary>
public record ComboBoxOption
{
    public required string DisplayName { get; init; }
    public Dictionary<string, object?>? ValueMappings { get; init; }
    public int? SimpleValue { get; init; }
    public bool? CommandValue { get; init; }
    public ScriptOption? Script { get; init; }
    public string? Tooltip { get; init; }
    public string? Warning { get; init; }
    public (string Title, string Message)? Confirmation { get; init; }
    public Dictionary<string, string>? ScriptVariables { get; init; }
    public bool IsDefault { get; init; }
    public bool IsRecommended { get; init; }
}
