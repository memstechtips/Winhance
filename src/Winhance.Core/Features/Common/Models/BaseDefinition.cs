using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

public abstract record BaseDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? GroupName { get; init; }
    public string? Icon { get; init; }
    public string? IconPack { get; init; } = "Material";
    public InputType InputType { get; init; } = InputType.Toggle;
    public bool IsWindows11Only { get; init; }
    public bool IsWindows10Only { get; init; }
    public int? MinimumBuildNumber { get; init; }
    public int? MaximumBuildNumber { get; init; }
    public IReadOnlyList<RegistrySetting> RegistrySettings { get; init; } = Array.Empty<RegistrySetting>();
    public string? RestartProcess { get; init; }
    public string? RestartService { get; init; }
    public bool RequiresRestart { get; init; }

    // Typed metadata (replaces untyped CustomProperties dictionary)
    public ComboBoxMetadata? ComboBox { get; init; }
    public NumericRangeMetadata? NumericRange { get; init; }
    public PowerRecommendation? Recommendation { get; init; }
    public Dictionary<int, Dictionary<string, bool>>? SettingPresets { get; init; }
    public Dictionary<string, string>? CrossGroupChildSettings { get; init; }
    public string? VersionCompatibilityMessage { get; init; }
    public bool DisableTooltip { get; init; }
}
