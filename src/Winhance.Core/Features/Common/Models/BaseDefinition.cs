using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

public abstract record BaseDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string GroupName { get; init; }
    public InputType InputType { get; init; } = InputType.Toggle;
    public bool IsWindows11Only { get; init; }
    public bool IsWindows10Only { get; init; }
    public int? MinimumBuildNumber { get; init; }
    public int? MaximumBuildNumber { get; init; }
    public List<RegistrySetting> RegistrySettings { get; init; } = new();
    public Dictionary<string, object> CustomProperties { get; init; } = new();
}