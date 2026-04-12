using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Typed metadata for ComboBox/Selection settings.
/// During the Recommended/Default refactor, the nine parallel dictionaries are being replaced by the
/// <see cref="Options"/> list of <see cref="ComboBoxOption"/> records. The old fields remain
/// marked [Obsolete] until every callsite is migrated; they are deleted in a later task.
/// </summary>
public record ComboBoxMetadata
{
    // New canonical shape. Made `required` in Task A14 after all migrations land.
    public IReadOnlyList<ComboBoxOption>? Options { get; init; }

    // Persisted: Custom state is a runtime synthetic option (CustomStateIndex), not part of Options.
    public bool SupportsCustomState { get; init; }
    public string? CustomStateDisplayName { get; init; }

    // -- Deprecated. Migrated to ComboBoxOption.* during the refactor.
    [Obsolete("Use Options[i].DisplayName. Will be removed in Phase A step 14.")]
    public string[]? DisplayNames { get; init; }
    [Obsolete("Use Options[i].ValueMappings. Will be removed in Phase A step 14.")]
    public Dictionary<int, Dictionary<string, object?>>? ValueMappings { get; init; }
    [Obsolete("Use Options[i].SimpleValue. Will be removed in Phase A step 14.")]
    public Dictionary<int, int>? SimpleValueMappings { get; init; }
    [Obsolete("Use Options[i].CommandValue. Will be removed in Phase A step 14.")]
    public Dictionary<int, bool>? CommandValueMappings { get; init; }
    [Obsolete("Use Options[i].Script. Will be removed in Phase A step 14.")]
    public Dictionary<int, ScriptOption>? ScriptMappings { get; init; }
    [Obsolete("Use Options[i].Tooltip. Will be removed in Phase A step 14.")]
    public string[]? OptionTooltips { get; init; }
    [Obsolete("Use Options[i].Warning. Will be removed in Phase A step 14.")]
    public Dictionary<int, string>? OptionWarnings { get; init; }
    [Obsolete("Use Options[i].Confirmation. Will be removed in Phase A step 14.")]
    public Dictionary<int, (string Title, string Message)>? OptionConfirmations { get; init; }
    [Obsolete("Use Options[i].ScriptVariables. Will be removed in Phase A step 14.")]
    public Dictionary<int, Dictionary<string, string>>? ScriptVariables { get; init; }
}
