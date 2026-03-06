using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Models;

public record ItemDefinition : BaseDefinition
{
    // Immutable definition properties
    public new InputType InputType { get; init; } = InputType.CheckBox;
    public string[]? AppxPackageName { get; init; }
    public string[]? WinGetPackageId { get; init; }
    public string? MsStoreId { get; init; }
    public string? CapabilityName { get; init; }
    public string? OptionalFeatureName { get; init; }
    public string? ChocoPackageId { get; init; }
    public bool CanBeReinstalled { get; init; } = true;
    public bool RequiresReboot { get; init; }
    public Func<string>? RemovalScript { get; init; }
    /// <summary>
    /// Pattern for registry DisplayName matching. Supports {version}, {arch}, {locale} placeholders.
    /// When set, compared against registry DisplayNames.
    /// </summary>
    public string? RegistryDisplayName { get; init; }
    /// <summary>
    /// Pattern for registry SubKeyName matching. Supports {version}, {arch}, {locale} placeholders.
    /// When set, compared against registry SubKeyNames (including SystemComponent=1 entries).
    /// </summary>
    public string? RegistrySubKeyName { get; init; }
    public string? RegistryUninstallSearchPattern { get; init; }
    /// <summary>
    /// Paths to check for existence (file or directory) as a detection fallback.
    /// Supports environment variables (e.g. %USERPROFILE%).
    /// </summary>
    public string[]? DetectionPaths { get; init; }
    public string[]? ProcessesToStop { get; init; }
    public string? WebsiteUrl { get; init; }
    public ExternalAppMetadata? ExternalApp { get; init; }

    // Mutable runtime state — set by AppLoadingService/AppStatusDiscoveryService,
    // proxied through AppItemViewModel for UI binding
    public bool IsInstalled { get; set; }
    public DetectionSource DetectedVia { get; set; }
}