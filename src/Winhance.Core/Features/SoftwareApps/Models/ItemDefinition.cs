using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Models;

public record ItemDefinition : BaseDefinition
{
    public new InputType InputType { get; init; } = InputType.CheckBox;
    public string? AppxPackageName { get; init; }
    public string[]? WinGetPackageId { get; init; }
    public string? MsStoreId { get; init; }
    public string? CapabilityName { get; init; }
    public string? OptionalFeatureName { get; init; }
    public string? ChocoPackageId { get; init; }
    public bool CanBeReinstalled { get; init; } = true;
    public bool RequiresReboot { get; init; }
    public Func<string>? RemovalScript { get; init; }
    public string[]? SubPackages { get; init; }
    public string? RegistryUninstallSearchPattern { get; init; }
    public string[]? ProcessesToStop { get; init; }
    public string? WebsiteUrl { get; init; }
    public bool IsInstalled { get; set; }
    public DetectionSource DetectedVia { get; set; }
    public bool IsSelected { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? LastOperationError { get; set; }
}