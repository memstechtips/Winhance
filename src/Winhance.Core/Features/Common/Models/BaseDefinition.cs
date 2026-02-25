using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

public abstract record BaseDefinition
{
    private static readonly IReadOnlyDictionary<string, object> EmptyCustomProperties =
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

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
    public IReadOnlyDictionary<string, object> CustomProperties { get; init; } = EmptyCustomProperties;
    public string? RestartProcess { get; init; }
    public string? RestartService { get; init; }
    public bool RequiresRestart { get; init; }
}
