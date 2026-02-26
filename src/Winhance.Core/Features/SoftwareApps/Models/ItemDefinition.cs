using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Models;

public record ItemDefinition : BaseDefinition
{
    // Immutable definition properties
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
    public ExternalAppMetadata? ExternalApp { get; init; }

    // Mutable runtime state — set by AppLoadingService/AppStatusDiscoveryService,
    // proxied through AppItemViewModel for UI binding
    public bool IsInstalled { get; set; }
    public DetectionSource DetectedVia { get; set; }
}