using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models;

public record ItemDefinition : BaseDefinition
{
    public InputType InputType { get; init; } = InputType.CheckBox;
    public string? AppxPackageName { get; init; }
    public string? WinGetPackageId { get; init; }
    public string? CapabilityName { get; init; }
    public string? OptionalFeatureName { get; init; }
    public string? ScoopPackageName { get; init; }
    public string Category { get; init; } = string.Empty;
    public bool CanBeReinstalled { get; init; } = true;
    public bool RequiresReboot { get; init; }
    public Func<string>? RemovalScript { get; init; }
    public string[]? SubPackages { get; init; }
    public bool IsInstalled { get; set; }
    public bool IsSelected { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? LastOperationError { get; set; }
}